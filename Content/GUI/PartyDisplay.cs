using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terramon.Content.Configs;
using Terramon.Content.GUI.Common;
using Terramon.Core.Loaders.UILoading;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace Terramon.Content.GUI;

public class PartyDisplay : SmartUIState
{
    private readonly PartySidebarSlot[] PartySlots = new PartySidebarSlot[6];
    public bool IsDraggingSlot;
    public PartySidebar Sidebar;

    public override bool Visible =>
        !Main.playerInventory && !Main.LocalPlayer.dead && TerramonPlayer.LocalPlayer.HasChosenStarter;

    public override int InsertionIndex(List<GameInterfaceLayer> layers)
    {
        return layers.FindIndex(layer => layer.Name.Equals("Vanilla: Radial Hotbars"));
    }

    public override void OnInitialize()
    {
        Sidebar = new PartySidebar(new Vector2(200, 486))
        {
            VAlign = 0.5f
        };
        Sidebar.Left.Set(-1, 0f);
        for (var i = 0; i < PartySlots.Length; i++)
        {
            var slot = new PartySidebarSlot(this, i);
            slot.Top.Set(71 * i + 12 * i - 2, 0f);
            Sidebar.Append(slot);
            PartySlots[i] = slot;
        }

        Append(Sidebar);
    }

    public void UpdateSlot(PokemonData data, int index)
    {
        PartySlots[index].SetData(data);
    }

    public void UpdateAllSlots(PokemonData[] partyData)
    {
        for (var i = 0; i < PartySlots.Length; i++) UpdateSlot(partyData[i], i);
    }

    public void ClearAllSlots()
    {
        for (var i = 0; i < PartySlots.Length; i++) UpdateSlot(null, i);
    }

    public void SwapSlotIndexes(int index1, int index2)
    {
        TerramonPlayer.LocalPlayer.SwapParty(index1, index2);
        PartySlots[index1].Index = index2;
        PartySlots[index2].Index = index1;
        var slot1 = PartySlots[index1];
        var slot2 = PartySlots[index2];
        PartySlots[index1] = slot2;
        PartySlots[index2] = slot1;

        PartySlots[index2].PlayIndexSound();
    }

    public override void SafeUpdate(GameTime gameTime)
    {
        Recalculate();
    }
}

public class PartySidebar : UIContainer
{
    private bool IsToggled = true;
    private bool KeyUp = true;
    private ITweener ToggleTween;

    public PartySidebar(Vector2 size) : base(size)
    {
    }

    public override void Update(GameTime gameTime)
    {
        //base.Update(gameTime);

        //below code is a modification of code in UIElement.Update()
        //create a static version of Elements so modification in BringSlotToTop doesn't cause errors
        var elements_static = new UIElement[Elements.Count];
        Elements.CopyTo(elements_static);
        foreach (var element in elements_static) element.Update(gameTime);

        var openKey = Main.keyState.IsKeyDown(Keys.F);
        switch (openKey)
        {
            case true when KeyUp:
            {
                KeyUp = false;
                if (Main.drawingPlayerChat) break;
                ToggleTween?.Kill();
                if (IsToggled)
                {
                    ToggleTween = Tween.To(() => Left.Pixels, x => Left.Pixels = x, -125, 0.5f).SetEase(Ease.OutExpo);
                    IsToggled = false;
                }
                else
                {
                    ToggleTween = Tween.To(() => Left.Pixels, x => Left.Pixels = x, 0, 0.5f).SetEase(Ease.OutExpo);
                    IsToggled = true;
                }

                break;
            }
            case false:
                KeyUp = true;
                break;
        }
    }

    public void ForceKillAnimation()
    {
        ToggleTween?.Kill();
        Left.Pixels = IsToggled ? 0 : -125;
        Recalculate();
    }

    public void BringSlotToTop(PartySidebarSlot slot)
    {
        var index = Elements.FindIndex(s => (PartySidebarSlot)s == slot);
        Elements.RemoveAt(index);
        Elements.Add(slot);
    }
}

public class PartySidebarSlot : UIImage
{
    private readonly UIText LevelText;
    private readonly UIText NameText;
    private readonly PartyDisplay PartyDisplay;
    private int _index;
    private PokemonData Data;
    private bool Dragging;
    private UIImage GenderIcon;
    private UIImage HeldItemBox;
    private bool IsHovered;
    private bool JustEndedDragging;
    public bool MonitorCursor;
    public UIMouseEvent MonitorEvent;
    private Vector2 Offset;
    private ITweener SnapTween;
    private UIImage SpriteBox;

    public PartySidebarSlot(PartyDisplay partyDisplay, int index) : base(ModContent.Request<Texture2D>(
        "Terramon/Assets/GUI/Party/SidebarClosed",
        AssetRequestMode.ImmediateLoad))
    {
        PartyDisplay = partyDisplay;
        Index = index;
        NameText = new UIText(string.Empty, 0.67f);
        NameText.Left.Set(8, 0);
        NameText.Top.Set(57, 0);
        Append(NameText);
        LevelText = new UIText(string.Empty, 0.67f);
        LevelText.Left.Set(8, 0);
        LevelText.Top.Set(10, 0);
        Append(LevelText);
    }

    public int Index
    {
        get => _index;
        set
        {
            SnapPosition(value);
            _index = value;
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        if (!IsMouseHovering || Data == null || PartyDisplay.IsDraggingSlot) return;
        var hoverText = Language.GetTextValue("Mods.Terramon.GUI.Party.SlotHover");
        if (TerramonPlayer.LocalPlayer.NextFreePartyIndex() > 1)
            hoverText += Language.GetTextValue("Mods.Terramon.GUI.Party.SlotHoverExtra");
        Main.hoverItemName = hoverText;
    }

    public void PlayIndexSound()
    {
        if (ModContent.GetInstance<ClientConfig>().ReducedAudio)
            return;

        var s = new SoundStyle
        {
            SoundPath = "Terramon/Assets/Audio/Sounds/button_smm",
            Pitch = (float)_index / -15 + 0.6f,
            Volume = 0.25f
        };
        SoundEngine.PlaySound(s);
    }

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);
        MonitorEvent = evt;
        MonitorCursor = true;
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);
        MonitorCursor = false;
        if (Dragging)
            DragEnd();
        else
            Main.NewText("Clicked on slot containing " + NameText.Text, Color.SeaGreen);
        //replace with whatever code to summon pokemon
    }

    public override void RightMouseDown(UIMouseEvent evt)
    {
        base.RightMouseDown(evt);
        DragStart(evt);
    }

    public override void RightMouseUp(UIMouseEvent evt)
    {
        base.RightMouseUp(evt);
        DragEnd();
    }

    private void SnapPosition(int index)
    {
        if (Data == null || Dragging) return;
        SnapTween = Tween.To(() => Top.Pixels, x => Top.Pixels = x, -2 + 83 * index, 0.15f).SetEase(Ease.OutExpo);
    }

    private void DragStart(UIMouseEvent evt)
    {
        if (Data == null || TerramonPlayer.LocalPlayer.NextFreePartyIndex() < 2) return;
        PlayIndexSound();
        PartyDisplay.Sidebar.BringSlotToTop(this);
        Offset = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);
        Dragging = true;
        PartyDisplay.IsDraggingSlot = true;
        SnapTween?.Kill();
    }

    private void DragEnd()
    {
        if (Data == null || TerramonPlayer.LocalPlayer.NextFreePartyIndex() < 2) return;
        if (ModContent.GetInstance<ClientConfig>().ReducedAudio)
            SoundEngine.PlaySound(SoundID.Tink);
        Dragging = false;
        JustEndedDragging = true;
        PartyDisplay.IsDraggingSlot = false;
        SnapPosition(Index);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (MonitorCursor)
            //check if mouse has travelled minimum distance in order to enter drag
            if (MathF.Abs(MonitorEvent.MousePosition.Length() - Main.MouseScreen.Length()) > 8)
            {
                MonitorCursor = false;
                DragStart(MonitorEvent);
            }

        var transparentColor = Color.White * 0.45f;
        if (PartyDisplay.IsDraggingSlot)
        {
            if (!Dragging)
            {
                if (Data == null) return;
                Color = transparentColor;
                NameText.TextColor = transparentColor;
                LevelText.TextColor = transparentColor;
                HeldItemBox.Color = transparentColor;
                SpriteBox.Color = transparentColor;
                ((UIImage)SpriteBox.Children.ElementAt(0)).Color = transparentColor;
                GenderIcon.Color = transparentColor;
                return;
            }

            var i = TerramonPlayer.LocalPlayer.NextFreePartyIndex() - 1;
            if (i == -2) i = 6;
            var bottomScrollMax = 71 * i + 12 * i - 2;
            var yOff = Math.Max(Math.Min(Main.mouseY - Offset.Y, bottomScrollMax), -2);
            switch (yOff)
            {
                case >= 45.5f when Index == 0:
                    PartyDisplay.SwapSlotIndexes(0, 1);
                    break;
                case < 45.5f when Index == 1:
                    PartyDisplay.SwapSlotIndexes(1, 0);
                    break;
                case >= 128.5f when Index == 1:
                    PartyDisplay.SwapSlotIndexes(1, 2);
                    break;
                case < 128.5f when Index == 2:
                    PartyDisplay.SwapSlotIndexes(2, 1);
                    break;
                case >= 211.5f when Index == 2:
                    PartyDisplay.SwapSlotIndexes(2, 3);
                    break;
                case < 211.5f when Index == 3:
                    PartyDisplay.SwapSlotIndexes(3, 2);
                    break;
                case >= 294.5f when Index == 3:
                    PartyDisplay.SwapSlotIndexes(3, 4);
                    break;
                case < 294.5f when Index == 4:
                    PartyDisplay.SwapSlotIndexes(4, 3);
                    break;
                case >= 377.5f when Index == 4:
                    PartyDisplay.SwapSlotIndexes(4, 5);
                    break;
                case < 377.5f when Index == 5:
                    PartyDisplay.SwapSlotIndexes(5, 4);
                    break;
            }

            Top.Set(yOff, 0f);
        }
        else if (Data != null)
        {
            Color = Color.White;
            NameText.TextColor = Color.White;
            LevelText.TextColor = Color.White;
            HeldItemBox.Color = Color.White;
            SpriteBox.Color = Color.White;
            ((UIImage)SpriteBox.Children.ElementAt(0)).Color = Color.White;
            GenderIcon.Color = Color.White;
        }

        if (IsMouseHovering && !PartyDisplay.IsDraggingSlot)
        {
            if (Data == null) return;
            Main.LocalPlayer.mouseInterface = true;
            if (IsHovered) return;
            IsHovered = true;
            if (!JustEndedDragging) SoundEngine.PlaySound(SoundID.MenuTick);
            UpdateSprite(true);
        }
        else
        {
            if (Data == null || !IsHovered) return;
            IsHovered = false;
            JustEndedDragging = false;
            UpdateSprite();
        }
    }

    private void UpdateSprite(bool selected = false)
    {
        var spritePath = "Terramon/Assets/GUI/Party/SidebarOpen";
        if (Data != null)
        {
            if (selected)
                spritePath += "_Selected";
        }
        else
        {
            spritePath = "Terramon/Assets/GUI/Party/SidebarClosed";
        }

        SetImage(ModContent.Request<Texture2D>(spritePath,
            AssetRequestMode.ImmediateLoad));
    }

    public void SetData(PokemonData data)
    {
        Data = data;
        UpdateSprite();
        HeldItemBox?.Remove();
        SpriteBox?.Remove();
        GenderIcon?.Remove();
        if (data == null)
        {
            NameText.SetText(string.Empty);
            LevelText.SetText(string.Empty);
        }
        else
        {
            NameText.SetText(Terramon.DatabaseV2.GetLocalizedPokemonName(data.ID).Value);
            LevelText.SetText("Lv. " + data.Level);
            HeldItemBox = new UIImage(ModContent.Request<Texture2D>("Terramon/Assets/GUI/Party/HeldItemBox",
                AssetRequestMode.ImmediateLoad));
            HeldItemBox.Top.Set(25, 0f);
            HeldItemBox.Left.Set(8, 0f);
            SpriteBox = new UIImage(ModContent.Request<Texture2D>("Terramon/Assets/GUI/Party/SpriteBox",
                AssetRequestMode.ImmediateLoad));
            SpriteBox.Top.Set(10, 0f);
            SpriteBox.Left.Set(59, 0f);
            var sprite = new UIImage(ModContent.Request<Texture2D>(
                $"Terramon/Assets/Pokemon/{Terramon.DatabaseV2.GetPokemonName(data.ID)}{(data.Variant != null ? "_" + data.Variant : string.Empty)}_Mini{(data.IsShiny ? "_S" : string.Empty)}",
                AssetRequestMode.ImmediateLoad))
            {
                ImageScale = 0.7f
            };
            sprite.Top.Set(-12, 0f);
            sprite.Left.Set(-20, 0f);
            SpriteBox.Append(sprite);
            var genderIconPath = data.Gender != Gender.Unspecified
                ? $"Terramon/Assets/GUI/Party/Icon{(data.Gender == Gender.Male ? "Male" : "Female")}"
                : "Terramon/Assets/Empty";
            GenderIcon = new UIBlendedImage(ModContent.Request<Texture2D>(genderIconPath,
                AssetRequestMode.ImmediateLoad));
            GenderIcon.Top.Set(57, 0f);
            GenderIcon.Left.Set(87, 0f);
            Append(HeldItemBox);
            Append(SpriteBox);
            Append(GenderIcon);
        }

        Recalculate();
    }
}
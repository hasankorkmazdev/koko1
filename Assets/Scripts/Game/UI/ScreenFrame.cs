using System;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Alt şeritten açılan tam ekranların çerçevesi: geri düğmesi, başlık ve alt yazı. Tesis Kur ile aynı görünür.
    public sealed class ScreenFrame
    {
        private const float StrokeBold = 2.4f;
        private const float BackGap = 8f;

        public readonly MetalPanel Root;
        public readonly Label Subtitle;

        public ScreenFrame(VisualElement frame, GameSession session, string title, Action close)
        {
            Root = new MetalPanel().WithGradient(UiBuilder.PanelBottom, UiBuilder.PanelBottom).WithRivets();
            Root.AddToClassList("gw-builder");
            Root.style.display = DisplayStyle.None;
            frame.Add(Root);

            MetalPanel header = new MetalPanel().WithGradient(UiBuilder.PanelTop, UiColors.HeaderBottom);
            header.AddToClassList("gw-builder-header");
            Root.Add(header);

            MetalPanel back = UiBuilder.MetalButton().WithHitOutset(UiBuilder.SmallButtonHitOutset, UiBuilder.SmallButtonHitOutset).WithClick(close);
            back.AddToClassList("gw-icon-button");
            back.style.marginRight = BackGap;
            var backIcon = new IconElement(session.Icons.Get("back"), UiColors.Text, StrokeBold);
            backIcon.AddToClassList("gw-back-icon");
            back.Add(backIcon);
            header.Add(back);

            VisualElement column = UiBuilder.Passive(header, "gw-panel-title-column");
            UiBuilder.Text(column, title, "gw-tag", "gw-tag-small");
            Subtitle = UiBuilder.Text(column, "", "gw-panel-sub-below");
        }

        public void SetVisible(bool visible)
        {
            Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Sekme ve miktar düğmeleri: seçili olan altın görünür.
        public static void StyleChoice(MetalPanel button, Label label, bool active)
        {
            button
                .WithGradient(active ? UiColors.ActiveTop : UiColors.ButtonTop, active ? UiColors.ActiveBottom : UiColors.ButtonBottom)
                .WithInsets(active ? UiColors.ActiveInset : UiColors.ButtonInset, UiColors.Clear);
            label.style.color = active ? UiColors.DarkText : UiColors.Text;
        }
    }
}

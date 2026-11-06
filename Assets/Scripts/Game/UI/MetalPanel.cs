using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // Eskimiş metal görünümü: dikey geçişli zemin, iç çizgiler, perçinler ve alt gölge.
    // UI Toolkit'te geçiş ve iç gölge olmadığı için doğrudan çizilir; sadece görünüm değişince yeniden çizilir.
    public class MetalPanel : VisualElement
    {
        private const int QuadVertices = 4;
        private const int QuadIndices = 6;
        private const float PressedBrightness = 0.8f;
        private const float RivetOffset = 7f;
        private const float RivetCoreRadius = 1.5f;
        private const float RivetRingRadius = 2.5f;
        private const float RivetBorderInset = 2f;
        private const float InsetThickness = 1f;
        private const float FullCircleDegrees = 360f;

        private static readonly ushort[] QuadOrder = { 0, 1, 2, 2, 3, 0 };

        private Color _top;
        private Color _bottom;
        private Color _insetTop = UiColors.Clear;
        private Color _insetBottom = UiColors.Clear;
        private bool _hasRivets;
        private float _shadowHeight;
        private float _hitOutsetX;
        private float _hitOutsetTop;
        private float _hitOutsetBottom;
        private bool _pressed;

        public MetalPanel()
        {
            generateVisualContent += Draw;
        }

        public MetalPanel WithGradient(Color top, Color bottom)
        {
            _top = top;
            _bottom = bottom;
            MarkDirtyRepaint();
            return this;
        }

        public MetalPanel WithInsets(Color top, Color bottom)
        {
            _insetTop = top;
            _insetBottom = bottom;
            MarkDirtyRepaint();
            return this;
        }

        public MetalPanel WithRivets()
        {
            _hasRivets = true;
            MarkDirtyRepaint();
            return this;
        }

        public MetalPanel WithShadow(float height)
        {
            _shadowHeight = height;
            MarkDirtyRepaint();
            return this;
        }

        // Düğme küçük görünse de dokunma alanı en az 44 birim olsun.
        public MetalPanel WithHitOutset(float x, float y)
        {
            return WithHitOutset(x, y, y);
        }

        // Alt alta duran kutuların dokunma alanı birbirine girmesin diye üst ve alt pay ayrı verilebilir.
        public MetalPanel WithHitOutset(float x, float top, float bottom)
        {
            _hitOutsetX = x;
            _hitOutsetTop = top;
            _hitOutsetBottom = bottom;
            return this;
        }

        // Geliştirme aracının dokunuşu buradan geçer.
        public Action ClickAction { get; private set; }

        public MetalPanel WithClick(Action action)
        {
            ClickAction = action;
            this.AddManipulator(new Clickable(action));
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnPointerRelease, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(OnPointerRelease, TrickleDown.TrickleDown);
            RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            return this;
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            Rect area = layout;
            return localPoint.x >= -_hitOutsetX
                && localPoint.x <= area.width + _hitOutsetX
                && localPoint.y >= -_hitOutsetTop
                && localPoint.y <= area.height + _hitOutsetBottom;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            SetPressed(true);
        }

        private void OnPointerRelease<T>(PointerEventBase<T> evt) where T : PointerEventBase<T>, new()
        {
            SetPressed(false);
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            SetPressed(false);
        }

        private void SetPressed(bool pressed)
        {
            if (_pressed != pressed)
            {
                _pressed = pressed;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = layout.width;
            float height = layout.height;
            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f)
            {
                return;
            }

            // Çerçeve zeminden önce çizilir; zemin çerçevenin içinde kalmazsa onu örter.
            float left = resolvedStyle.borderLeftWidth;
            float top = resolvedStyle.borderTopWidth;
            float innerWidth = width - left - resolvedStyle.borderRightWidth;
            float innerHeight = height - top - resolvedStyle.borderBottomWidth;

            AddQuad(context, left, top, innerWidth, innerHeight, Shade(_top), Shade(_bottom));

            if (_insetTop.a > 0f)
            {
                AddQuad(context, left, top, innerWidth, InsetThickness, _insetTop, _insetTop);
            }

            if (_insetBottom.a > 0f)
            {
                AddQuad(context, left, top + innerHeight - InsetThickness, innerWidth, InsetThickness, _insetBottom, _insetBottom);
            }

            if (_shadowHeight > 0f)
            {
                AddQuad(context, 0f, height, width, _shadowHeight, UiColors.Shadow, UiColors.Clear);
            }

            if (_hasRivets)
            {
                DrawRivetBorder(context, width, height);
                DrawRivets(context.painter2D, width, height);
            }
        }

        private Color Shade(Color color)
        {
            return _pressed
                ? new Color(color.r * PressedBrightness, color.g * PressedBrightness, color.b * PressedBrightness, color.a)
                : color;
        }

        private static void DrawRivetBorder(MeshGenerationContext context, float width, float height)
        {
            float inset = RivetBorderInset;
            float inner = width - inset * 2f;
            float tall = height - inset * 2f;
            Color color = UiColors.RivetBorder;
            AddQuad(context, inset, inset, inner, InsetThickness, color, color);
            AddQuad(context, inset, height - inset - InsetThickness, inner, InsetThickness, color, color);
            AddQuad(context, inset, inset, InsetThickness, tall, color, color);
            AddQuad(context, width - inset - InsetThickness, inset, InsetThickness, tall, color, color);
        }

        private static void DrawRivets(Painter2D painter, float width, float height)
        {
            DrawRivet(painter, RivetOffset, RivetOffset);
            DrawRivet(painter, width - RivetOffset, RivetOffset);
            DrawRivet(painter, RivetOffset, height - RivetOffset);
            DrawRivet(painter, width - RivetOffset, height - RivetOffset);
        }

        private static void DrawRivet(Painter2D painter, float x, float y)
        {
            DrawDisc(painter, x, y, RivetRingRadius, UiColors.RivetRing);
            DrawDisc(painter, x, y, RivetCoreRadius, UiColors.RivetCore);
        }

        private static void DrawDisc(Painter2D painter, float x, float y, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(new Vector2(x, y), radius, Angle.Degrees(0f), Angle.Degrees(FullCircleDegrees));
            painter.ClosePath();
            painter.Fill();
        }

        public static void AddQuad(MeshGenerationContext context, float x, float y, float width, float height, Color top, Color bottom)
        {
            MeshWriteData mesh = context.Allocate(QuadVertices, QuadIndices);
            mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + width, y, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + width, y + height, Vertex.nearZ), tint = bottom });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x, y + height, Vertex.nearZ), tint = bottom });
            for (int i = 0; i < QuadOrder.Length; i++)
            {
                mesh.SetNextIndex(QuadOrder[i]);
            }
        }
    }

    // Haritanın kenarlarını koyulaştırır.
    public sealed class VignetteElement : VisualElement
    {
        private const float Depth = 40f;

        public VignetteElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = layout.width;
            float height = layout.height;
            if (float.IsNaN(width) || float.IsNaN(height))
            {
                return;
            }

            MetalPanel.AddQuad(context, 0f, 0f, width, Depth, UiColors.Vignette, UiColors.Clear);
            MetalPanel.AddQuad(context, 0f, height - Depth, width, Depth, UiColors.Clear, UiColors.Vignette);
        }
    }
}

using System;
using UnityEngine;

namespace GeoWar.Game
{
    // Tek parmakla kaydırma, iki parmakla yakınlaştırma. Bilgisayarda fare ve tekerlek de çalışır.
    public sealed class MapCameraController : MonoBehaviour
    {
        private const float TapSlopDesign = 6f;
        private const float WheelZoomIn = 1.15f;
        private const float WheelZoomOut = 0.87f;
        private const float PanSlackDesign = 320f;
        private const float AnimationSeconds = 0.38f;
        private const float CameraDistance = 10f;

        private Camera _camera;
        private Vector2 _lastPointer;
        private Vector2 _pressStart;
        private bool _isDragging;
        private bool _movedSincePress;
        private bool _pointerBlocked;
        private float _lastPinchDistance;

        private bool _isAnimating;
        private float _animationTime;
        private Vector2 _animationFromCenter;
        private Vector2 _animationToCenter;
        private float _animationFromZoom;
        private float _animationToZoom;

        // Dokunuş arayüzün üstündeyse harita tepki vermez.
        public Func<Vector2, bool> IsOverUi;

        public event Action<Vector2> Tapped;

        // Kamera oynayınca açılır; etiketleri güncelleyen taraf okuyup kapatır.
        public bool Changed { get; set; }

        public Vector2 Center
        {
            get { return _camera.transform.position; }
        }

        public float Zoom { get; private set; }

        // Son gerçek dokunuşun zamanı: geliştirme aracı, kullanıcı oynarken oyunu kesmemek için bakar.
        public float LastInputTime { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugPan(float dxDesign, float dyDesign)
        {
            _isAnimating = false;
            SetView(Center + new Vector2(-dxDesign / Zoom, dyDesign / Zoom), Zoom);
        }

        public void DebugZoom(float factor)
        {
            ZoomAround(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), factor);
        }

        public void DebugTap(Vector2 screenPoint)
        {
            if (Tapped != null)
            {
                Tapped(_camera.ScreenToWorldPoint(screenPoint));
            }
        }
#endif

        // Pencere boyutu değişince aynı yakınlıkta kalır.
        public void OnScreenChanged()
        {
            SetView(Center, Zoom);
        }

        public void Configure(Camera targetCamera, Vector2 worldPoint, float zoom, float focusY)
        {
            // Dokunuşlar ayrıca fare olayı üretmesin, yoksa bir dokunuş iki kez sayılabilir.
            Input.simulateMouseWithTouches = false;
            _camera = targetCamera;
            _camera.orthographic = true;
            SetView(CenterFor(worldPoint, zoom, focusY), zoom);
        }

        // Verilen noktayı ekranda istenen yüksekliğe getiren kamera merkezi.
        public static Vector2 CenterFor(Vector2 worldPoint, float zoom, float focusY)
        {
            float offset = (focusY - MapLayout.ViewHeight * 0.5f) / zoom;
            return new Vector2(worldPoint.x, worldPoint.y + offset);
        }

        public void AnimateTo(Vector2 center, float zoom)
        {
            _isAnimating = true;
            _animationTime = 0f;
            _animationFromCenter = Center;
            _animationFromZoom = Zoom;
            _animationToCenter = center;
            _animationToZoom = Mathf.Clamp(zoom, MapLayout.MinZoom, MapLayout.MaxZoom);
        }

        public void AnimateZoom(float factor)
        {
            AnimateTo(Center, Zoom * factor);
        }

        // Kendi başına değil, oyun döngüsünden çağrılır: kamera önce oynar, yazılar aynı karede arkasından gelir.
        // İkisi ayrı çalışsaydı sıraları değişebilir, yazılar haritadan bir kare geride kalırdı.
        public void Tick()
        {
            if (_isAnimating)
            {
                StepAnimation(Time.unscaledDeltaTime);
            }

            if (Input.anyKeyDown || Input.touchCount > 0 || Mathf.Abs(Input.mouseScrollDelta.y) > 0f)
            {
                LastInputTime = Time.realtimeSinceStartup;
            }

            if (Input.touchSupported && Input.touchCount > 0)
            {
                HandleTouches();
                return;
            }

            if (Input.touchSupported)
            {
                _pointerBlocked = false;
            }

            HandleMouse();
        }

        private void StepAnimation(float deltaTime)
        {
            _animationTime += deltaTime;
            float t = Mathf.Clamp01(_animationTime / AnimationSeconds);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            float zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(_animationFromZoom), Mathf.Log(_animationToZoom), eased));
            SetView(Vector2.Lerp(_animationFromCenter, _animationToCenter, eased), zoom);
            if (t >= 1f)
            {
                _isAnimating = false;
            }
        }

        private void HandleTouches()
        {
            if (Input.touchCount >= 2)
            {
                if (_pointerBlocked)
                {
                    return;
                }

                Touch first = Input.GetTouch(0);
                Touch second = Input.GetTouch(1);
                float distance = Vector2.Distance(first.position, second.position);
                if (first.phase == TouchPhase.Began || second.phase == TouchPhase.Began || _lastPinchDistance <= 0f)
                {
                    _lastPinchDistance = distance;
                    _isAnimating = false;
                }
                else if (distance > 0f)
                {
                    ZoomAround((first.position + second.position) * 0.5f, distance / _lastPinchDistance);
                    _lastPinchDistance = distance;
                }

                _movedSincePress = true;
                _isDragging = false;
                return;
            }

            _lastPinchDistance = 0f;
            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _pointerBlocked = IsBlocked(touch.position);
                    BeginPress(touch.position);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    MovePress(touch.position);
                    break;
                case TouchPhase.Ended:
                    EndPress(touch.position);
                    break;
                case TouchPhase.Canceled:
                    _isDragging = false;
                    break;
            }
        }

        private void HandleMouse()
        {
            Vector2 pointer = Input.mousePosition;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0f && !IsBlocked(pointer))
            {
                ZoomAround(pointer, wheel > 0f ? WheelZoomIn : WheelZoomOut);
            }

            if (Input.GetMouseButtonDown(0))
            {
                _pointerBlocked = IsBlocked(pointer);
                BeginPress(pointer);
            }
            else if (Input.GetMouseButton(0))
            {
                MovePress(pointer);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                EndPress(pointer);
            }
        }

        private bool IsBlocked(Vector2 pointer)
        {
            return IsOverUi != null && IsOverUi(pointer);
        }

        private void BeginPress(Vector2 pointer)
        {
            if (_pointerBlocked)
            {
                return;
            }

            _isAnimating = false;
            _pressStart = pointer;
            _lastPointer = pointer;
            _isDragging = true;
            _movedSincePress = false;
        }

        private void MovePress(Vector2 pointer)
        {
            if (!_isDragging)
            {
                return;
            }

            if (!_movedSincePress && Vector2.Distance(pointer, _pressStart) < TapSlopDesign * MapLayout.UiScale)
            {
                return;
            }

            _movedSincePress = true;
            Vector2 delta = (Vector2)_camera.ScreenToWorldPoint(_lastPointer) - (Vector2)_camera.ScreenToWorldPoint(pointer);
            SetView(Center + delta, Zoom);
            _lastPointer = pointer;
        }

        private void EndPress(Vector2 pointer)
        {
            if (_isDragging && !_movedSincePress && Tapped != null)
            {
                Tapped(_camera.ScreenToWorldPoint(pointer));
            }

            _isDragging = false;
        }

        // Parmakların arasındaki nokta yerinde kalacak şekilde yakınlaştırır.
        private void ZoomAround(Vector2 screenPoint, float factor)
        {
            _isAnimating = false;
            Vector2 before = _camera.ScreenToWorldPoint(screenPoint);
            float zoom = Mathf.Clamp(Zoom * factor, MapLayout.MinZoom, MapLayout.MaxZoom);
            _camera.orthographicSize = MapLayout.ViewHeight / (2f * zoom);
            Vector2 after = _camera.ScreenToWorldPoint(screenPoint);
            SetView(Center + before - after, zoom);
        }

        private void SetView(Vector2 center, float zoom)
        {
            zoom = Mathf.Clamp(zoom, MapLayout.MinZoom, MapLayout.MaxZoom);
            float slackX = (MapLayout.ViewWidth * 0.5f - PanSlackDesign) / zoom;
            float slackY = (MapLayout.ViewHeight * 0.5f - PanSlackDesign) / zoom;
            Rect bounds = MapLayout.PanBounds;

            Zoom = zoom;
            _camera.orthographicSize = MapLayout.ViewHeight / (2f * zoom);
            _camera.transform.position = new Vector3(
                ClampAxis(center.x, bounds.xMin + slackX, bounds.xMax - slackX),
                ClampAxis(center.y, bounds.yMin + slackY, bounds.yMax - slackY),
                -CameraDistance);
            Changed = true;
        }

        private static float ClampAxis(float value, float min, float max)
        {
            return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
        }
    }
}

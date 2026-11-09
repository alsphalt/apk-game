using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sim.Controls
{
    // On-screen driving controls built at runtime: steering wheel (drag), gas, brake, handbrake,
    // camera and tilt buttons. Multi-touch works because each control is its own UI element.
    public class TouchControls : MonoBehaviour
    {
        public bool tiltSteering;
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Handbrake { get; private set; }
        public float Steer => wheel ? wheel.Value : 0f;

        bool cameraPressed;
        PedalButton gas, brake, hand;
        SteeringWheel wheel;
        Text tiltLabel;

        public bool ConsumeCamera() { bool p = cameraPressed; cameraPressed = false; return p; }

        public void Build(Canvas canvas)
        {
            var root = canvas.transform;

            var wheelRt = UiKit.Rect(root, "Wheel", new Vector2(0, 0), new Vector2(70, 50), new Vector2(380, 380));
            var wheelImg = wheelRt.gameObject.AddComponent<Image>();
            wheelImg.sprite = UiKit.Ring(); wheelImg.color = new Color(1, 1, 1, 0.45f);
            var mark = UiKit.Panel(wheelRt, "Marker", new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(34, 56), new Color(1f, 0.3f, 0.2f, 0.9f));
            mark.raycastTarget = false;
            wheel = wheelRt.gameObject.AddComponent<SteeringWheel>();
            wheel.rt = wheelRt;

            gas = Pedal(root, "Gas", "GAS", new Vector2(-50, 60), new Vector2(190, 320), new Color(0.2f, 0.7f, 0.3f, 0.55f));
            brake = Pedal(root, "Brake", "BRAKE", new Vector2(-270, 60), new Vector2(190, 200), new Color(0.85f, 0.2f, 0.2f, 0.55f));
            hand = Pedal(root, "Hand", "HAND", new Vector2(-270, 280), new Vector2(190, 100), new Color(0.9f, 0.6f, 0.1f, 0.55f));

            UiKit.Button(root, "Cam", new Vector2(1, 1), new Vector2(-40, -40), new Vector2(130, 130), "CAM", 30,
                new Color(0.1f, 0.1f, 0.1f, 0.6f), () => cameraPressed = true);
            var tilt = UiKit.Button(root, "Tilt", new Vector2(1, 1), new Vector2(-190, -40), new Vector2(130, 130), "TILT\nOFF", 26,
                new Color(0.1f, 0.1f, 0.1f, 0.6f), null);
            tiltLabel = tilt.GetComponentInChildren<Text>();
            tilt.onClick.AddListener(() =>
            {
                tiltSteering = !tiltSteering;
                tiltLabel.text = tiltSteering ? "TILT\nON" : "TILT\nOFF";
            });
        }

        static PedalButton Pedal(Transform root, string name, string text, Vector2 pos, Vector2 size, Color color)
        {
            var img = UiKit.Panel(root, name, new Vector2(1, 0), pos, size, color);
            var p = img.gameObject.AddComponent<PedalButton>();
            UiKit.Label(img.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, size, text, 34, TextAnchor.MiddleCenter, Color.white);
            return p;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Throttle = Mathf.MoveTowards(Throttle, gas && gas.Held ? 1f : 0f, (gas && gas.Held ? 5f : 8f) * dt);
            Brake = Mathf.MoveTowards(Brake, brake && brake.Held ? 1f : 0f, (brake && brake.Held ? 6f : 10f) * dt);
            Handbrake = hand && hand.Held ? 1f : 0f;
        }
    }

    public class PedalButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Held { get; private set; }
        public void OnPointerDown(PointerEventData e) => Held = true;
        public void OnPointerUp(PointerEventData e) => Held = false;
        void OnDisable() => Held = false;
    }

    // Drag around the wheel to steer; it springs back to centre when released.
    public class SteeringWheel : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform rt;
        public float maxAngle = 135f;
        public float Value { get; private set; }

        float angle, last;
        bool dragging;

        float PointerAngle(PointerEventData e)
        {
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rt.position);
            Vector2 d = e.position - center;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;      // clockwise from up
        }

        public void OnPointerDown(PointerEventData e) { dragging = true; last = PointerAngle(e); }

        public void OnDrag(PointerEventData e)
        {
            float a = PointerAngle(e);
            angle = Mathf.Clamp(angle + Mathf.DeltaAngle(last, a), -maxAngle, maxAngle);
            last = a;
        }

        public void OnPointerUp(PointerEventData e) => dragging = false;

        void Update()
        {
            if (!dragging) angle = Mathf.MoveTowards(angle, 0f, 450f * Time.unscaledDeltaTime);
            Value = angle / maxAngle;
            rt.localEulerAngles = new Vector3(0f, 0f, -angle);
        }
    }
}

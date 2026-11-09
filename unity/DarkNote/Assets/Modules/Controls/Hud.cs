using Sim.Vehicles;
using UnityEngine;
using UnityEngine.UI;

namespace Sim.Controls
{
    // Speed, gear and rev bar. Built at runtime on a canvas.
    public class Hud : MonoBehaviour
    {
        public VehicleController car;
        Text speed, gear, title;
        RectTransform rpmFill;
        Image rpmImage;

        public void Build(Canvas canvas)
        {
            var root = canvas.transform;
            var a = new Vector2(0.5f, 0f);
            UiKit.Panel(root, "RpmBack", a, new Vector2(0, 40), new Vector2(520, 22), new Color(0, 0, 0, 0.5f));
            var fill = UiKit.Panel(root, "RpmFill", new Vector2(0, 0), Vector2.zero, new Vector2(520, 22), Color.white);
            // Anchored to the same bottom-centre spot, but sized by hand each frame.
            rpmFill = fill.rectTransform;
            rpmFill.anchorMin = rpmFill.anchorMax = a; rpmFill.pivot = new Vector2(0f, 0f);
            rpmFill.anchoredPosition = new Vector2(-260, 40);
            rpmImage = fill;

            speed = UiKit.Label(root, "Speed", a, new Vector2(0, 70), new Vector2(300, 90), "0", 80, TextAnchor.LowerCenter, Color.white);
            UiKit.Label(root, "Unit", a, new Vector2(0, 66), new Vector2(300, 30), "km/h", 24, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.7f));
            gear = UiKit.Label(root, "Gear", a, new Vector2(230, 70), new Vector2(120, 90), "N", 70, TextAnchor.LowerCenter, new Color(1f, 0.85f, 0.3f));
            title = UiKit.Label(root, "Name", new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(800, 40), "", 30, TextAnchor.UpperCenter, new Color(1, 1, 1, 0.85f));
        }

        public void SetName(string n) { if (title) title.text = n; }

        void Update()
        {
            if (!car || !speed) return;
            speed.text = Mathf.RoundToInt(car.SpeedKmh).ToString();
            int g = car.Box.Gear;
            gear.text = g < 0 ? "R" : g == 0 ? "N" : g.ToString();
            float r = Mathf.Clamp01(car.RpmNormalized);
            rpmFill.sizeDelta = new Vector2(520f * r, 22f);
            rpmImage.color = Color.Lerp(new Color(0.3f, 0.8f, 1f), new Color(1f, 0.2f, 0.15f), Mathf.InverseLerp(0.75f, 0.95f, r));
        }
    }
}

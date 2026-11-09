using System;
using System.Collections.Generic;
using System.Linq;
using Sim.Controls;
using Sim.Vehicles;
using UnityEngine;
using UnityEngine.UI;

namespace Sim.Game
{
    // Runtime garage: pick a brand and car, change paint, toggle upgrade parts.
    public class GarageUI : MonoBehaviour
    {
        public VehicleCatalog catalog;
        public VehicleCustomizer customizer;           // set by the bootstrap after each spawn
        public List<PartDefinition> parts = new List<PartDefinition>();
        public event Action<VehicleDefinition> CarChosen;

        string brand;
        int page;
        const int PerPage = 7;
        RectTransform panel, dyn;
        VehicleDefinition current;
        bool open;

        static readonly Color[] Paints =
        {
            new Color(0.78f, 0.12f, 0.10f), new Color(0.95f, 0.95f, 0.95f), new Color(0.05f, 0.05f, 0.06f),
            new Color(0.10f, 0.25f, 0.65f), new Color(0.95f, 0.75f, 0.05f), new Color(0.15f, 0.55f, 0.25f),
            new Color(0.55f, 0.55f, 0.58f), new Color(0.90f, 0.35f, 0.05f),
        };

        public void Build(Canvas canvas, VehicleDefinition start)
        {
            current = start;
            brand = start ? start.brand : catalog.Brands().FirstOrDefault();

            UiKit.Button(canvas.transform, "GarageBtn", new Vector2(0, 1), new Vector2(30, -30), new Vector2(210, 90),
                "GARAGE", 32, new Color(0.1f, 0.1f, 0.1f, 0.65f), Toggle);

            panel = UiKit.Panel(canvas.transform, "GaragePanel", new Vector2(0, 1), new Vector2(30, -140),
                new Vector2(760, 900), new Color(0.04f, 0.05f, 0.07f, 0.88f)).rectTransform;
            dyn = UiKit.Rect(panel, "Dyn", new Vector2(0, 1), Vector2.zero, new Vector2(760, 900));
            panel.gameObject.SetActive(false);
        }

        public void SetCurrent(VehicleDefinition def, VehicleCustomizer c)
        {
            current = def; customizer = c;
            if (open) Refresh();
        }

        void Toggle()
        {
            open = !open;
            panel.gameObject.SetActive(open);
            if (open) Refresh();
        }

        Button B(string text, float x, float y, float w, float h, Color col, UnityEngine.Events.UnityAction act, int size = 26) =>
            UiKit.Button(dyn, text, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h), text, size, col, act);

        void Refresh()
        {
            for (int i = dyn.childCount - 1; i >= 0; i--) Destroy(dyn.GetChild(i).gameObject);
            var dim = new Color(0.2f, 0.22f, 0.28f, 0.95f);
            var hi = new Color(0.15f, 0.45f, 0.85f, 0.95f);

            // Brand tabs
            var brands = catalog.Brands();
            float bx = 20f, bw = (720f - 10f * (brands.Length - 1)) / Mathf.Max(1, brands.Length);
            foreach (var b in brands)
            {
                string bb = b;
                B(bb, bx, 20, bw, 70, bb == brand ? hi : dim, () => { brand = bb; page = 0; Refresh(); });
                bx += bw + 10f;
            }

            // Car list with paging
            var cars = catalog.OfBrand(brand);
            int pages = Mathf.Max(1, Mathf.CeilToInt(cars.Length / (float)PerPage));
            page = Mathf.Clamp(page, 0, pages - 1);
            float y = 105f;
            foreach (var d in cars.Skip(page * PerPage).Take(PerPage))
            {
                var def = d;
                B(def.model + "  " + def.year, 20, y, 720, 58, current == def ? hi : dim, () => Choose(def), 24);
                y += 64f;
            }
            B("<", 20, y, 90, 56, dim, () => { page = (page + pages - 1) % pages; Refresh(); });
            UiKit.Label(dyn, "Page", new Vector2(0, 1), new Vector2(380, -y - 12), new Vector2(200, 30), (page + 1) + " / " + pages, 24, TextAnchor.MiddleCenter, Color.white);
            B(">", 650, y, 90, 56, dim, () => { page = (page + 1) % pages; Refresh(); });
            y += 70f;

            if (current)
            {
                UiKit.Label(dyn, "Stats", new Vector2(0, 1), new Vector2(380, -y - 14), new Vector2(720, 30),
                    $"{current.PeakPowerHp():F0} hp   {current.massKg:F0} kg   {current.drive}   {current.layout}" +
                    (current.turbo ? " turbo" : current.supercharged ? " supercharged" : ""),
                    24, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.85f));
                y += 44f;
            }

            // Paint
            float px = 20f;
            foreach (var c in Paints)
            {
                var col = c;
                var btn = B("", px, y, 78, 78, col, () => Paint(col));
                px += 90f;
            }
            y += 92f;

            // Parts
            foreach (var p in parts)
            {
                var part = p;
                bool on = customizer && customizer.Has(part);
                B((on ? "[x] " : "[  ] ") + part.displayName, 20, y, 720, 52, on ? hi : dim, () => TogglePart(part), 22);
                y += 58f;
            }
        }

        void Choose(VehicleDefinition d)
        {
            current = d;
            CarChosen?.Invoke(d);
            Refresh();
        }

        void Paint(Color c)
        {
            if (!customizer) return;
            customizer.SetPaint(c, 0.6f, 0.85f);
            Persist();
        }

        void TogglePart(PartDefinition p)
        {
            if (!customizer) return;
            if (customizer.Has(p)) customizer.Remove(p.slot); else customizer.Install(p);
            Persist();
            Refresh();
        }

        void Persist()
        {
            if (customizer && current) SaveSystem.SaveBuild(current.id, customizer.ToJson());
        }
    }
}

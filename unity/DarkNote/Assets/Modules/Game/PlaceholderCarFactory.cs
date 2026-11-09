using Sim.Traffic;
using Sim.Vehicles;
using UnityEngine;

namespace Sim.Game
{
    // Builds drivable boxy cars from a VehicleDefinition so physics, city and traffic can be
    // tested before any art exists. Real cars replace this with a prefab per definition
    // (same hierarchy: wheel colliders, wheel meshes, PaintTarget on the body).
    public static class PlaceholderCarFactory
    {
        static Material Mat(Color c, float smooth = 0.5f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }

        static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static void Dims(VehicleDefinition d, out float length, out float width, out float height)
        {
            length = d.wheelbase + 1.95f;
            width = 1.85f;
            switch (d.body)
            {
                case BodyType.Truck: height = 1.95f; width = 2.0f; length += 0.9f; break;
                case BodyType.SUV: height = 1.75f; width = 1.95f; break;
                case BodyType.Super: height = 1.15f; width = 1.95f; break;
                case BodyType.Sports: height = 1.25f; break;
                case BodyType.Coupe: height = 1.35f; break;
                case BodyType.Wagon: height = 1.47f; break;
                default: height = 1.45f; break;
            }
            if (d.massKg < 1100f) width = 1.65f;
        }

        // Returns an inactive car. Caller positions nothing: pass the ground point and heading.
        public static GameObject Build(VehicleDefinition def, Vector3 groundPoint, Quaternion rot, Color paint)
        {
            Dims(def, out float L, out float W, out float H);
            float r = def.wheelRadius;
            float wheelTopY = 0.15f;
            float groundLocalY = wheelTopY - def.travel * 0.5f - r;       // ground relative to car origin

            var root = new GameObject(def.DisplayName);
            root.SetActive(false);
            root.AddComponent<Rigidbody>();
            var car = root.AddComponent<VehicleController>();
            car.def = def;

            var bodyMat = Mat(paint, 0.8f);
            float clearance = 0.18f;
            float lowerH = H * 0.55f, upperH = H * 0.42f;
            float y0 = groundLocalY + clearance;
            var lower = Cube(root.transform, "Body", new Vector3(0f, y0 + lowerH * 0.5f, 0f), new Vector3(W, lowerH, L), bodyMat);
            var cabin = Cube(root.transform, "Cabin", new Vector3(0f, y0 + lowerH + upperH * 0.5f, -L * 0.06f),
                new Vector3(W * 0.88f, upperH, L * (def.body == BodyType.Truck ? 0.33f : 0.5f)), bodyMat);
            Cube(root.transform, "Glass", new Vector3(0f, y0 + lowerH + upperH * 0.5f, -L * 0.06f),
                new Vector3(W * 0.9f, upperH * 0.6f, L * (def.body == BodyType.Truck ? 0.30f : 0.46f)), Mat(new Color(0.05f, 0.07f, 0.1f), 0.9f));
            foreach (var g in new[] { lower, cabin })
            {
                var pt = g.AddComponent<PaintTarget>();
                pt.target = g.GetComponent<Renderer>();
            }

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(W, H * 0.9f, L);
            col.center = new Vector3(0f, y0 + H * 0.45f, 0f);

            float track = W - 0.25f, wb = def.wheelbase;
            var wheelMat = Mat(new Color(0.08f, 0.08f, 0.08f), 0.2f);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2, left = i % 2 == 0;
                var pos = new Vector3(left ? -track * 0.5f : track * 0.5f, wheelTopY, front ? wb * 0.5f : -wb * 0.5f);

                var wcGo = new GameObject("WheelCollider" + i);
                wcGo.transform.SetParent(root.transform, false);
                wcGo.transform.localPosition = pos;
                car.wheels[i] = wcGo.AddComponent<WheelCollider>();

                var holder = new GameObject("WheelMesh" + i).transform;
                holder.SetParent(root.transform, false);
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(cyl.GetComponent<Collider>());
                cyl.transform.SetParent(holder, false);
                cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                cyl.transform.localScale = new Vector3(r * 2f, 0.13f, r * 2f);
                cyl.GetComponent<Renderer>().sharedMaterial = wheelMat;
                car.wheelMeshes[i] = holder;
            }

            root.AddComponent<VehicleCustomizer>();
            root.AddComponent<SynthEngineAudio>();

            root.transform.SetPositionAndRotation(groundPoint + Vector3.up * (-groundLocalY + 0.25f), rot);
            return root;
        }

        public static TrafficCar BuildTraffic(Transform parent, Color color)
        {
            var root = new GameObject("TrafficCar");
            root.transform.SetParent(parent, false);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var bc = root.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.8f, 1.4f, 4.4f); bc.center = new Vector3(0f, 0.75f, 0f);
            var m = Mat(color, 0.6f);
            Cube(root.transform, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.7f, 4.4f), m);
            Cube(root.transform, "Cabin", new Vector3(0f, 1.08f, -0.2f), new Vector3(1.6f, 0.5f, 2.2f), m);
            root.AddComponent<TrafficCar>();
            root.SetActive(false);
            return root.GetComponent<TrafficCar>();
        }
    }
}

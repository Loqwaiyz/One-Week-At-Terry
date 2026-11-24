using UnityEngine;
using UnityEngine.Rendering;

public enum ParkMascot { Terry, Larry, Umi, Kesha }

internal static class ParkMascots
{
    public const int SurveillanceLayer = 30;

    public static Transform Create(ParkMascot kind, Material material)
    {
        bool lion = kind == ParkMascot.Terry;
        var actor = new GameObject(lion ? "Лев Терри" : kind == ParkMascot.Umi ? "Уми" : kind == ParkMascot.Kesha ? "Попугай Кеша" : "Черепаха Ларри").transform;
        Color amber = new Color(1f, .68f, .2f), brown = new Color(.57f,.21f,.07f);
        Color green = new Color(.32f,.74f,.41f), shell = new Color(.14f,.39f,.25f);
        void Part(string name, Vector3 position, Vector3 size, Color color)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            g.transform.SetParent(actor,false);
            g.transform.localPosition = position;
            g.transform.localScale = size;
            var collider = g.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            var renderer = g.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(tint);
            if (kind != ParkMascot.Larry)
            {
                g.layer = SurveillanceLayer;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        if (kind == ParkMascot.Kesha)
        {
            Color plumage = new Color(.92f,.23f,.12f);
            Part("Хвост", new Vector3(0,1.15f,-.35f), new Vector3(.4f,1.15f,.4f), Color.blue);
            Part("Туловище", new Vector3(0,1.8f,0), new Vector3(.85f,1.2f,.75f), plumage);
            Part("Голова", new Vector3(0,2.43f,.08f), Vector3.one*.72f, plumage);
            Part("Клюв", new Vector3(0,2.36f,.47f), new Vector3(.39f,.35f,.48f), Color.yellow);
            foreach (int side in new[]{-1,1})
                Part("Крыло", new Vector3(side*.42f,1.75f,0), new Vector3(.3f,.87f,.65f), Color.cyan);
            return actor;
        }
        if (kind == ParkMascot.Umi)
        {
            Color fur = new Color(.86f,.76f,.94f);
            Part("Туловище", new Vector3(0,.54f,0), new Vector3(.6f,.95f,.52f), fur);
            Part("Голова", new Vector3(0,1.08f,.08f), Vector3.one*.59f, fur);
            foreach (int side in new[]{-1,1})
            {
                Part("Ухо", new Vector3(side*.16f,1.53f,.07f), new Vector3(.14f,.64f,.16f), fur);
                Part("Глаз", new Vector3(side*.11f,1.12f,.34f), Vector3.one*.07f, Color.black);
            }
            return actor;
        }
        if (lion)
        {
            Part("Туловище", new Vector3(0,1.35f,0), new Vector3(1.2f,2,1), amber);
            Part("Грива", new Vector3(0,2.62f,0), new Vector3(1.75f,1.75f,1.15f), brown);
            Part("Морда", new Vector3(0,2.62f,.47f), new Vector3(1.1f,1.1f,.68f), amber);
        }
        else
        {
            Part("Панцирь", new Vector3(0,.75f,0),new Vector3(2.05f,1.4f,2.45f),shell);
            Part("Голова", new Vector3(0,.95f,1.05f),new Vector3(.98f,.95f,1.15f),green);
        }
        foreach (int side in new[]{-1,1})
        {
            Part("Глаз",new Vector3(side*(lion?.24f:.2f),lion?2.75f:1.11f,lion?.76f:1.51f), Vector3.one*.18f,Color.black);
            Part("Лапа",new Vector3(side*(lion?.4f:.85f),.31f,.18f),new Vector3(.52f,.65f,.8f),lion?amber:green);
        }
        return actor;
    }
}

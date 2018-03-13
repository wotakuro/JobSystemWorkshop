using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class BoardRenderer : MonoBehaviour {

    private Renderer renderer;
    public Rect rect = new Rect(0, 0, 1, 1);
    private MaterialPropertyBlock prop;

    void Awake() {
        renderer = this.GetComponent<Renderer>();
        prop = new MaterialPropertyBlock();
    }

    public void SetMaterial(Material material)
    {
        renderer.material = material;
    }

    public void SetRect(Rect r)
    {
        Vector4 val = new Vector4( r.x,r.y,r.width,r.height);
        prop.SetVector( ShaderNameHash.RectValue, val);
        renderer.SetPropertyBlock(prop);
        this.rect = r;
    }
}

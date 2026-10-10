using UnityEngine;

namespace GeometryRhythm
{
    // Project a rigid door swinging about its fixed outer hinge into the pixel stage.
    // Both hinge vertices remain fixed; the free edge describes an arc toward the viewer.
    public sealed class DormitoryHingedDoor : MonoBehaviour
    {
        Mesh mesh;
        Material material;
        readonly Vector3[] vertices=new Vector3[4];
        float width,height,sign;
        public void Initialize(Sprite sprite,bool right)
        {
            width=sprite.rect.width/sprite.pixelsPerUnit;height=sprite.rect.height/sprite.pixelsPerUnit;sign=right?-1:1;
            mesh=new Mesh{name="Rigid hinged door leaf"};
            var filter=gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sortingOrder=2;
            material=new Material(Shader.Find("Sprites/Default"));material.mainTexture=sprite.texture;renderer.sharedMaterial=material;
            Rect r=sprite.rect;float x0=r.x/sprite.texture.width,x1=r.xMax/sprite.texture.width,y0=r.y/sprite.texture.height,y1=r.yMax/sprite.texture.height;
            mesh.vertices=vertices;
            mesh.uv=new[]{new Vector2(right?x1:x0,y0),new Vector2(right?x0:x1,y0),new Vector2(right?x0:x1,y1),new Vector2(right?x1:x0,y1)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};
            SetOpen(0);
        }
        public void SetOpen(float openness)
        {
            float angle=Mathf.Clamp01(openness)*78*Mathf.Deg2Rad;
            float depth=width*Mathf.Sin(angle),projection=6/(6-depth),eyeY=height*1.35f;
            float freeX=sign*width*Mathf.Cos(angle)*projection;
            vertices[0]=Vector3.zero;vertices[3]=new Vector3(0,height,0);
            vertices[1]=new Vector3(freeX,eyeY*(1-projection),0);
            vertices[2]=new Vector3(freeX,eyeY+(height-eyeY)*projection,0);
            mesh.vertices=vertices;mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}

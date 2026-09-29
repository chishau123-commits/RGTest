using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm.Editor
{
    public static class HudLayoutValidation
    {
        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            int checks=0;
            var oldSafe=MobileUiLayout.SimulatedSafeArea;
            var root=new GameObject("HUD layout regression");
            var camera=root.AddComponent<Camera>();
            RenderTexture texture=null;
            try
            {
                var hud=new DemoHud(root.transform,()=>{},()=>{},()=>{},()=>{});
                hud.UseCaptureCamera(camera);
                foreach(var size in new[]{new Vector2Int(3048,2032),new Vector2Int(1920,1080),new Vector2Int(2400,1080)})
                foreach(bool inset in new[]{false,true})
                {
                    camera.targetTexture=null;
                    if(texture!=null) UnityEngine.Object.DestroyImmediate(texture);
                    texture=new RenderTexture(size.x,size.y,0);camera.targetTexture=texture;
                    var safe=inset?new Rect(.04f,.03f,.93f,.95f):new Rect(0,0,1,1);
                    MobileUiLayout.SimulatedSafeArea=new Rect(safe.x*Screen.width,safe.y*Screen.height,safe.width*Screen.width,safe.height*Screen.height);
                    Canvas.ForceUpdateCanvases();
                    typeof(DemoHud).GetMethod("RefreshLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
                    Canvas.ForceUpdateCanvases();
                    Rect top=Bounds(root.transform,"Top dock",camera),bottom=Bounds(root.transform,"Bottom status",camera);
                    Check(Mathf.Abs(top.yMax-size.y)<1,"top dock reaches screen top: "+top+" viewport="+size,ref checks);
                    Check(Mathf.Abs(bottom.yMin)<1,"bottom dock reaches screen bottom: "+bottom+" viewport="+size,ref checks);
                    Check(Mathf.Abs(top.xMin)<1&&Mathf.Abs(top.xMax-size.x)<1,"top background covers screen width",ref checks);
                    Check(Mathf.Abs(bottom.xMin)<1&&Mathf.Abs(bottom.xMax-size.x)<1,"bottom background covers screen width",ref checks);
                    Rect pause=Bounds(root.transform,"Pause",camera),score=Bounds(root.transform,"Score",camera),status=Bounds(root.transform,"Section",camera);
                    Check(pause.xMin>=safe.xMin*size.x-1&&pause.yMax<=safe.yMax*size.y+1,"pause respects safe area",ref checks);
                    Check(score.xMax<=safe.xMax*size.x+1&&score.yMax<=safe.yMax*size.y+1,"score respects safe area",ref checks);
                    Check(status.xMin>=safe.xMin*size.x-1&&status.yMin>=safe.yMin*size.y-1,"footer text respects safe area",ref checks);
                    foreach(var label in root.GetComponentsInChildren<Text>(true))
                        if(label.name=="State"||label.name=="Audio sync value")
                            Check(label.preferredHeight<=label.rectTransform.rect.height+1,"pause text fits vertically: "+label.name,ref checks);
                }
                Debug.Log("HUD_LAYOUT_VALIDATION_SUCCESS "+checks+" checks");
            }
            finally
            {
                MobileUiLayout.SimulatedSafeArea=oldSafe;
                camera.targetTexture=null;
                if(texture!=null) UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static Rect Bounds(Transform root,string name,Camera camera)
        {
            foreach(var r in root.GetComponentsInChildren<RectTransform>(true))
                if(r.name==name)
                {
                    var corners=new Vector3[4];r.GetWorldCorners(corners);
                    Vector2 a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
                    Vector2 b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                    return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
                }
            throw new Exception("HUD element missing: "+name);
        }
        static void Check(bool condition,string message,ref int checks)
        { checks++;if(!condition)throw new Exception("HUD layout: "+message); }
    }
}

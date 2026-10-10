using UnityEngine;

namespace GeometryRhythm
{
    public sealed partial class DormitoryDuskScene
    {
        public const float OutdoorConversationSize=4.6f;
        public bool OutdoorDialogueActive {get;private set;}
        bool outdoorCameraReturning;
        public bool OutdoorInputLocked=>OutdoorDialogueActive||outdoorCameraReturning;
        public Transform OutdoorPlayer=>actor;
        public float ActualCameraSize=>stageCamera.orthographicSize;
        float outdoorCameraTime,outdoorCameraFromSize,outdoorCameraTargetSize;
        Vector3 outdoorCameraFrom,outdoorCameraTarget;

        // Absolute framing, independent of the player's previous zoom; groups share a bounding frame.
        public void FocusOutdoorSpeakers(params Transform[] speakers)
        {
            if(stageCamera==null||speakers==null||speakers.Length==0)return;
            var bounds=new Bounds();bool found=false;
            foreach(var speaker in speakers)
            {
                if(speaker==null)continue;
                Vector3 point=transform.InverseTransformPoint(speaker.position)+Vector3.up*.9f;
                if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
            }
            if(!found)return;
            float size=Mathf.Max(OutdoorConversationSize,bounds.size.x/(stageCamera.aspect*1.4f)+1,bounds.size.y+2);
            Vector3 target=new Vector3(bounds.center.x,bounds.center.y-.2f,-20);
            float halfWidth=background.GetComponent<SpriteRenderer>().sprite.bounds.extents.x*background.localScale.x;
            float limit=Mathf.Max(0,halfWidth-size*stageCamera.aspect);
            target.x=Mathf.Clamp(target.x,-limit,limit);
            float halfHeight=background.GetComponent<SpriteRenderer>().sprite.bounds.extents.y;
            target.y=Mathf.Clamp(target.y,-Mathf.Max(0,halfHeight-size),Mathf.Max(0,halfHeight-size));
            if(OutdoorDialogueActive&&(target-outdoorCameraTarget).sqrMagnitude<.0001f&&
                Mathf.Abs(size-outdoorCameraTargetSize)<.001f)return;
            outdoorCameraFrom=stageCamera.transform.localPosition;outdoorCameraFromSize=stageCamera.orthographicSize;
            outdoorCameraTarget=target;outdoorCameraTargetSize=size;outdoorCameraTime=0;
            OutdoorDialogueActive=true;outdoorCameraReturning=false;hasMoveTarget=false;
        }

        public void EndOutdoorDialogue()
        {
            if(!OutdoorDialogueActive)return;
            outdoorCameraFrom=stageCamera.transform.localPosition;outdoorCameraFromSize=stageCamera.orthographicSize;
            outdoorCameraTime=0;OutdoorDialogueActive=false;outdoorCameraReturning=true;
        }

        void UpdateOutdoorCamera(float dt)
        {
            if(!OutdoorInputLocked)return;
            outdoorCameraTime+=dt;
            float raw=Mathf.Clamp01(outdoorCameraTime/(OutdoorDialogueActive?1.05f:.75f));
            float ease=raw*raw*raw*(raw*(raw*6-15)+10);
            Vector3 target=OutdoorDialogueActive?outdoorCameraTarget:stageCamera.transform.localPosition;
            float size=OutdoorDialogueActive?outdoorCameraTargetSize:stageCamera.orthographicSize;
            stageCamera.transform.localPosition=Vector3.Lerp(outdoorCameraFrom,target,ease);
            stageCamera.orthographicSize=Mathf.Lerp(outdoorCameraFromSize,size,ease);
            if(outdoorCameraReturning&&raw>=1)outdoorCameraReturning=false;
        }
    }
}

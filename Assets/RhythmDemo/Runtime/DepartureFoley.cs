using UnityEngine;

namespace GeometryRhythm
{
    // Short, deterministic action sounds. No external service or replacement music is used.
    public static class DepartureFoley
    {
        public static AudioClip Create(string kind)
        {
            float duration=kind=="coin"?.75f:kind=="motor"?3.6f:kind=="slide"?.7f:kind=="ding"?1.1f:kind=="pia"?.48f:.24f;
            const int rate=44100;var samples=new float[Mathf.CeilToInt(duration*rate)];
            var random=new System.Random(kind.Length*719+kind[0]);float low=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate,u=t/duration,n=(float)random.NextDouble()*2-1;
                low=Mathf.Lerp(low,n,.12f);float value;
                switch(kind)
                {
                    case "coin":
                        value=0;
                        for(int hit=0;hit<4;hit++)
                        {
                            float age=t-(hit==0?0:hit==1?.11f:hit==2?.26f:.43f);
                            if(age<0)continue;
                            float tone=Mathf.Sin(age*2*Mathf.PI*(2310+hit*137))+.46f*Mathf.Sin(age*2*Mathf.PI*3671);
                            value+=(tone*.22f*Mathf.Exp(-age*38)+n*.1f*Mathf.Exp(-age*140))*(1-hit*.16f);
                        }
                        break;
                    case "pia": value=(n*.55f*Mathf.Exp(-t*95)+Mathf.Sin(2*Mathf.PI*(180*t-130*t*t))*.45f*Mathf.Exp(-t*17));break;
                    case "step": value=(low*.75f+Mathf.Sin(t*2*Mathf.PI*92)*.2f)*Mathf.Exp(-t*28);break;
                    case "door": value=(low*.6f+Mathf.Sin(t*2*Mathf.PI*65)*.3f)*Mathf.Exp(-t*19);break;
                    case "chair": value=(low*.6f+Mathf.Sin(t*2*Mathf.PI*270)*.12f)*Mathf.Sin(Mathf.PI*u);break;
                    case "slide": value=low*.45f*Mathf.Sin(Mathf.PI*u)+Mathf.Sin(t*2*Mathf.PI*140)*.05f*Mathf.Sin(Mathf.PI*u);break;
                    case "motor": value=(Mathf.Sin(t*2*Mathf.PI*54)+Mathf.Sin(t*2*Mathf.PI*108)*.3f+low*.3f)*.12f*Mathf.Sin(Mathf.PI*u);break;
                    case "ding": value=(Mathf.Sin(t*2*Mathf.PI*880)+.35f*Mathf.Sin(t*2*Mathf.PI*1320))*.24f*Mathf.Exp(-t*4);break;
                    default:value=(n*.5f+Mathf.Sin(t*2*Mathf.PI*720)*.2f)*Mathf.Exp(-t*65);break;
                }
                samples[i]=Mathf.Clamp(value*Mathf.Min(1,t/.002f)*Mathf.Min(1,(duration-t)/.01f),-.85f,.85f);
            }
            var clip=AudioClip.Create("Departure / "+kind,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
    }
}

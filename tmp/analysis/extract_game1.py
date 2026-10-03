from pathlib import Path
import imageio_ffmpeg,subprocess,json
from PIL import Image,ImageDraw,ImageFont
root=Path(r'D:\MyDataInD\Quad\tmp\analysis\game1');root.mkdir(exist_ok=True)
src=r'C:\Users\Chinshyo\Desktop\新建文件夹\Game1.mp4'
ff=imageio_ffmpeg.get_ffmpeg_exe()
subprocess.run([ff,'-hide_banner','-loglevel','error','-i',src,'-vf','fps=4,scale=768:-1',str(root/'f%03d.png')],check=True)
font=ImageFont.truetype(r'C:\Windows\Fonts\msyh.ttc',18)
files=sorted(root.glob('f*.png'))
for start in range(0,len(files),20):
    batch=files[start:start+20];sheet=Image.new('RGB',(1920,5*292),(14,18,27));d=ImageDraw.Draw(sheet)
    for j,p in enumerate(batch):
        im=Image.open(p);im.thumbnail((480,264));x=(j%4)*480;y=(j//4)*292;sheet.paste(im,(x,y+26));d.text((x+12,y+1),f'Game1   {(start+j)/4:.2f} s',font=font,fill='white')
    sheet.save(root/f'sheet{start//20+1}.jpg',quality=92)
print(json.dumps({'frames':len(files),'sheets':list(map(str,root.glob('sheet*.jpg')))}))

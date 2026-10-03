from PIL import Image, ImageDraw
import subprocess
from pathlib import Path
import imageio_ffmpeg

out = Path(r'D:\MyDataInD\Quad\tmp\analysis\game3')
src = r'C:\Users\Chinshyo\Desktop\新建文件夹\Game3.mp4'
ff = imageio_ffmpeg.get_ffmpeg_exe()
ranges = [(0,1.1),(2.8,3.9),(7.3,9.0),(12.5,13.7)]
for p,(a,b) in enumerate(ranges):
    times = [round(a + i*.1,2) for i in range(round((b-a)*10)+1)]
    sheet = Image.new('RGB', (1660, 266*((len(times)+3)//4)), 'black')
    draw = ImageDraw.Draw(sheet)
    for j,t in enumerate(times):
        f=out/f'dense_{p}_{t:05.2f}.png'
        subprocess.run([ff,'-v','error','-ss',str(t),'-i',src,'-frames:v','1',str(f),'-y'],check=True)
        im=Image.open(f).resize((415,236))
        x,y=(j%4)*415,(j//4)*266
        sheet.paste(im,(x,y))
        draw.text((x+5,y+238),f'{t:05.2f}s',fill='white')
    sheet.save(out/f'dense_contact_{p}.png')

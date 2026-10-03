from pathlib import Path
import math, json, html, re
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor, Color, black, white
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, Table, TableStyle
from reportlab.lib.styles import ParagraphStyle

ROOT=Path(r'D:\MyDataInD\Quad'); OUT=ROOT/'output/pdf/圆环节奏游戏核心计划书.pdf'
pdfmetrics.registerFont(TTFont('YH',r'C:\Windows\Fonts\msyh.ttc',subfontIndex=0))
pdfmetrics.registerFont(TTFont('YHB',r'C:\Windows\Fonts\msyhbd.ttc',subfontIndex=0))
W,H=A4; X=43; CW=W-2*X; C=canvas.Canvas(str(OUT),pagesize=A4,pageCompression=1)
C.setTitle('圆环节奏游戏核心计划书');C.setAuthor('项目策划');C.setSubject('ADOFAI协议借鉴 2026年10月17日验收 AI并行制作 Android 制谱器 iOS源码')
BLUE=HexColor('#265C86'); DARK=HexColor('#101B2D'); CYAN=HexColor('#55DEDF'); PURPLE=HexColor('#B5A2FA'); GRAY=HexColor('#586576')
S=ParagraphStyle('body',fontName='YH',fontSize=10.1,leading=16.2,wordWrap='CJK',textColor=black,spaceAfter=8)
SM=ParagraphStyle('small',parent=S,fontSize=8.9,leading=13.8)
TS=ParagraphStyle('table',parent=S,fontSize=8.8,leading=13.4,spaceAfter=0)
TH=ParagraphStyle('thead',parent=TS,fontName='YHB',textColor=white)
y=0; page_n=0; layouts=[]; code_blocks=[]
def txt(x,yy,s,size=10,color=black,bold=False,center=False):
    C.setFont('YHB' if bold else 'YH',size);C.setFillColor(color)
    (C.drawCentredString if center else C.drawString)(x,yy,s)
def footer():
    global y
    if y<50: raise RuntimeError(f'Page {page_n} overflow y={y}')
    layouts.append({'page':page_n,'bottom':round(y,1)})
    txt(X,26,'圆环节奏游戏  核心计划书  修订版  /  2026年10月3日',8,GRAY)
    txt(W-X,26,f'{page_n:02d}',8,GRAY,center=True)
def page(title,tag=''):
    global page_n,y
    if page_n: footer();C.showPage()
    page_n+=1;C.bookmarkPage(f'p{page_n}');C.addOutlineEntry(title,f'p{page_n}',0,False)
    txt(X,H-40,'UNITY 2022.3.62f3c1   •   ANDROID / iOS',8.5,GRAY)
    txt(X,H-76,title,21,black,True)
    y=H-99
    if tag: p(tag,small=True)
def p(text,small=False):
    global y
    obj=Paragraph(html.escape(text).replace('\n','<br/>'),SM if small else S);_,h=obj.wrap(CW,800)
    obj.drawOn(C,X,y-h);y-=h+8
def sub(text):
    global y
    y-=4;txt(X,y,text,12.1,black,True);y-=22
def table(headers,rows,widths):
    global y
    data=[[Paragraph(html.escape(str(a)),TH) for a in headers]]+[[Paragraph(html.escape(str(a)).replace('\n','<br/>'),TS) for a in r] for r in rows]
    t=Table(data,colWidths=[CW*z for z in widths],hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),BLUE),('ROWBACKGROUNDS',(0,1),(-1,-1),[white,HexColor('#F1F5F8')]),('GRID',(0,0),(-1,-1),.45,HexColor('#D9D9D9')),('VALIGN',(0,0),(-1,-1),'MIDDLE'),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6)]))
    _,h=t.wrap(CW,800);t.drawOn(C,X,y-h);y-=h+14
def figure(fn,h,caption):
    global y
    C.saveState();C.translate(X,y-h);fn(CW,h);C.restoreState();y-=h+6;p(caption,True)
def code(text,size=8.5):
    global y
    code_blocks.append(text.strip('\n'))
    lines=text.strip('\n').splitlines();lead=size*1.4;hh=len(lines)*lead+20
    if max(map(len,lines))>102: raise RuntimeError('Long code line')
    C.setFillColor(HexColor('#F3F5F7'));C.roundRect(X,y-hh,CW,hh,5,fill=1,stroke=0)
    C.setFont('Courier',size);C.setFillColor(black)
    for i,line in enumerate(lines):C.drawString(X+10,y-15-i*lead,line)
    y-=hh+12
def box(x,yy,w,h,title,detail='',fill=HexColor('#F0F5F8'),color=black):
    C.setFillColor(fill);C.setStrokeColor(HexColor('#D1DDE6'));C.roundRect(x,yy,w,h,6,fill=1,stroke=1)
    txt(x+w/2,yy+h-20,title,9.7,color,True,True)
    for j,line in enumerate(detail.split('\n')):txt(x+w/2,yy+h-38-j*14,line,8.5,color,center=True)
def arrow(x1,y1,x2,y2,color=BLUE):
    C.setStrokeColor(color);C.setFillColor(color);C.setLineWidth(1.2);C.line(x1,y1,x2,y2)
    a=math.atan2(y2-y1,x2-x1);p0=C.beginPath();p0.moveTo(x2,y2);p0.lineTo(x2-6*math.cos(a-.5),y2-6*math.sin(a-.5));p0.lineTo(x2-6*math.cos(a+.5),y2-6*math.sin(a+.5));p0.close();C.drawPath(p0,fill=1,stroke=0)
def ring(x,yy,r,color=CYAN,width=2,fill=False,label=''):
    C.setStrokeColor(color);C.setFillColor(Color(color.red,color.green,color.blue,.13));C.setLineWidth(width);C.circle(x,yy,r,stroke=1,fill=int(fill))
    if label:txt(x,yy-4,label,11,color,True,True)
def dark(w,h):C.setFillColor(DARK);C.roundRect(0,0,w,h,9,fill=1,stroke=0)
def mechanics(w,h):
    dark(w,h)
    for row in range(2):
        cy=h-78-row*134
        txt(15,cy+51,'外环收缩  SHRINK' if row==0 else '圆移动到环  ARRIVAL',10.4,white,True)
        for j,x in enumerate([96,252,408]):
            if row==0:
                ring(x,cy,22,CYAN,2,True);ring(x,cy,[46,32,22][j],PURPLE,2)
            else:
                ring(x+20,cy,23,PURPLE,2);mx=x+[-32,-7,20][j];ring(mx,cy,23,CYAN,2,True)
                if j<2:arrow(mx+8,cy,x+14,cy,GRAY)
            txt(x,cy-63,['提前出现','接近目标','边缘重合  点击'][j],8.7,white,center=True)
        if row==0:arrow(151,cy,191,cy);arrow(307,cy,347,cy)
def gameplay(w,h):
    dark(w,h)
    C.setStrokeColor(HexColor('#233853'))
    for xx in range(25,int(w),38):C.line(xx,28,xx,h-32)
    for yy in range(30,int(h)-20,38):C.line(20,yy,w-20,yy)
    txt(18,h-24,'示意曲名   NORMAL 06',9,white,True);txt(w-125,h-24,'98.64%   II',9,white)
    C.setFillColor(HexColor('#23435A'));C.roundRect(20,h-43,w-40,3,1,fill=1,stroke=0)
    C.setFillColor(CYAN);C.roundRect(20,h-43,(w-40)*.46,3,1,fill=1,stroke=0)
    ring(w*.29,h*.47,24,CYAN,2,True,'01');ring(w*.29,h*.47,45,CYAN,1)
    ring(w*.68,h*.58,25,PURPLE,2,False,'02');ring(w*.48,h*.28,25,CYAN,2,True)
    C.setStrokeColor(GRAY);C.setDash(3,3);C.bezier(w*.48,h*.28,w*.57,h*.33,w*.62,h*.38,w*.68,h*.58);C.setDash()
    ring(w*.83,h*.30,21,PURPLE,1,False,'03');ring(w*.83,h*.30,43,PURPLE,1)
    txt(w*.29,h*.47-63,'固定圆  外环收缩',8,white,center=True)
    txt(w*.69,h*.58+37,'点接收环  不追移动圆',8,white,center=True)
    txt(w/2,27,'128 COMBO       PERFECT  -12 ms',10,white,center=True)
    txt(20,8,'横屏布局  目标随玩法镜头移动并保持轮廓清楚',7.5,HexColor('#ADBFCC'))
def editor(w,h):
    dark(w,h);bar=h-34
    txt(12,h-21,'文件   编辑   谱面   检查   导出',9,white);txt(w-141,h-21,'预览   自动保存已启用',8.5,CYAN)
    C.setFillColor(HexColor('#1E3047'));C.rect(7,110,70,h-150,fill=1,stroke=0);C.rect(w-120,110,113,h-150,fill=1,stroke=0)
    for j,t in enumerate(['选择','缩圈音符','到位音符','路径','相机轨道','特效轨道']):txt(15,bar-26-j*26,t,8.7,white)
    txt(w-111,bar-20,'属性  n002',9,white,True)
    for j,t in enumerate(['判定  2:1:000','预读  2 拍','目标  850,360','半径  52','路径  p01','多押组  无','运镜  玩法层']):txt(w-111,bar-43-j*19,t,8.1,white)
    ring(w*.40,h*.58,26,CYAN,2,True,'01');ring(w*.40,h*.58,45,CYAN,1)
    ring(w*.61,h*.53,25,PURPLE,2,False,'02');arrow(w*.44,h*.40,w*.59,h*.49,CYAN)
    txt(87,121,'安全区  16:9 / 触区预览 / 原始画面与清晰模式',7.7,white)
    C.setFillColor(HexColor('#22364C'));C.rect(7,8,w-14,91,fill=1,stroke=0)
    for j,t in enumerate(['音频波形','音符','镜头','特效']):
        yy=79-j*18;txt(15,yy,t,8,white);C.setStrokeColor(GRAY);C.line(80,yy-4,w-18,yy-4)
        for k in range(12):
            xx=91+k*31
            if j==0:C.setStrokeColor(CYAN);C.line(xx,yy-5,xx,yy+((k*7)%11))
            elif k%3==j%3:ring(xx,yy,2.5,CYAN if j==1 else PURPLE,1)
    C.setStrokeColor(HexColor('#F5CE7E'));C.line(w*.54,12,w*.54,97)
def architecture(w,h):
    bw=145;xs=[0,(w-bw)/2,w-bw]
    labels=[('曲包与校验','JSON / 媒体 / 能力版本'),('谱面编译器','tick → 歌曲时间\n空间与轨道索引'),('歌曲时钟','DSP 预约起点\n输入时钟桥接'),('触控与判定','Began 样本 → 仲裁\n成绩 / 回放'),('确定性求值','音符 / 相机 / 特效\n绝对时间采样'),('渲染与反馈','保护层 / 背景层\n粒子池 / HUD')]
    for j,(t,d) in enumerate(labels):box(xs[j%3],h-85-(j//3)*112,bw,78,t,d)
    arrow(145,h-45,xs[1]-5,h-45)
    arrow(xs[1]+72,h-90,xs[1]+72,h-118)
    arrow(xs[1]+25,h-90,145,h-118)
    arrow(xs[2]+38,h-90,xs[1]+130,h-118)
    arrow(xs[1]+145,h-158,xs[2]-5,h-158)
    C.setStrokeColor(BLUE);C.line(72,h-199,72,19);C.line(72,19,xs[2]+72,19);arrow(xs[2]+72,19,xs[2]+72,h-200)
    txt(w/2,3,'游戏与制谱预览共用编译器  时钟  轨道求值器',9,BLUE,True,True)
def storyboard(w,h):
    ww=(w-12)/2;hh=(h-15)/2
    for j,stage in enumerate(['引子  暗蓝雾层','主段  目标随镜头移动','高潮  旋转与缩放冲击','休止  转场后重定位']):
        xx=(j%2)*(ww+12);yy=h-hh-(j//2)*(hh+15)
        C.setFillColor([HexColor('#17283D'),HexColor('#1C3B55'),HexColor('#3F4D76'),HexColor('#162536')][j]);C.roundRect(xx,yy,ww,hh,6,fill=1,stroke=0)
        for k in range(12):ring(xx+12+(k*37)%int(ww-24),yy+15+(k*23)%int(hh-35),1,GRAY,.5)
        rot,scale,shift=[(0,1,(0,0)),(0,1.05,(23,-3)),(-28,1.28,(-8,4)),(0,1,(-25,0))][j]
        a=math.radians(rot)
        for bx,by,col,label in [(ww*.38,hh*.46,CYAN,'1'),(ww*.7,hh*.56,PURPLE,'2')]:
            dx=bx-ww/2;dy=by-hh/2
            rx=ww/2+scale*(dx*math.cos(a)-dy*math.sin(a))+shift[0]
            ry=hh/2+scale*(dx*math.sin(a)+dy*math.cos(a))+shift[1]
            ring(xx+rx,yy+ry,15*scale,col,2,False,label)
        txt(xx+12,yy+hh-21,stage,9,white,True)
        txt(xx+12,yy+8,['目标保持轮廓','强运镜同样可以判定','触点匹配对应画面姿态','重定位与新段预告'][j],8,white)
def refgrid(items):
    global y
    ww=(CW-12)/2;hh=ww*866/1534
    for row in range((len(items)+1)//2):
        for col in range(2):
            i=row*2+col
            if i>=len(items):continue
            path,label=items[i];xx=X+col*(ww+12);C.drawImage(str(path),xx,y-hh,width=ww,height=hh,preserveAspectRatio=True,anchor='c');txt(xx,y-hh-14,label,8.2,GRAY)
        y-=hh+30
def flow(w,h):
    labels=['选曲与难度','教学或校准','加载与倒数','触屏演奏','成绩与重试'];bw=(w-40)/5
    for j,t in enumerate(labels):box(j*(bw+10),h-69,bw,60,t)
    for j in range(4):arrow(j*(bw+10)+bw+1,h-39,(j+1)*(bw+10)-2,h-39)
    txt(w/2,7,'暂停与设置覆盖层  独立接收输入  返回时重新倒数',9,BLUE,center=True)

page('圆环节奏游戏核心计划书','面向触屏演奏的游戏系统 制谱工具与谱面数据协议')
p('本项目使用 Unity 2022.3.62f3c1，最终面向 Android 与 iOS 触屏演奏。玩家在圆与圆环边缘重合时点击目标，音符采用外环收缩与圆移动到接收环两种提示。谱面组织借鉴已核查的 ADOFAI 数据结构；本次在 2026年10月17日 23:59 前验收 Android 与基础制谱器，并交付 iOS 源码工程。')
figure(gameplay,274,'设计图 1  手机游戏候选界面  所有界面图为本项目原创功能示意')
table(['需求状态','内容'],[['已确认','横屏；多押按设备能力；临判定可强运镜移动目标；查ADOFAI；尽可能用AI'],['期限与交付','10月17日最终验收Android与无需Unity的独立电脑制谱器；iOS交源码'],['投入与边界','每天4–8小时以上；当前无Mac/苹果设备；平板实时录入后续实现；本轮只交计划书'],['测试候选','窗口、触区、镜头轨迹与显示对齐误差通过原型确认，不虚构实测精度']], [.19,.81])
p('建议先建立“听到节拍、看懂提示、准确点中”的可玩核心，再增加演出。每首歌以同一份谱面驱动游戏与制谱预览，避免工具里可读、手机里难以点击的分歧。',True)

page('阅读路径与设计边界')
table(['阅读目标','页码与内容'],[['理解参考与产品范围','3–5  视频分析  核心体验  期限内范围'],['评审玩法与表现','6–12  两种音符  触控判定  界面  特效运镜'],['评审 Unity 实现路线','13–16  音频时钟  模块结构  生命周期  移动端'],['评审制谱能力','17–19  工具布局  工作流程  编辑与诊断'],['先核查 ADOFAI 再设计协议','20–22  来源  结构  时间语义  借鉴映射'],['对接本项目协议','23–30  曲包  时间  字段  完整 JSON 示例  校验'],['评审后续实时录入','31–32  电脑与平板  采集协议  时间同步'],['落实截止与 AI 制作','33–39  验证  10月17日验收  每日计划  AI分工  风险'],['核对资料','40–42  Unity 与 ADOFAI 来源索引']], [.34,.66])
sub('三层约定')
p('需求层记录已经提出或确认的目标；设计层给出本计划书推荐的工作方案；验证层记录必须靠手机原型与用户测试决定的数值。未回答的设计分岔保留候选标签，不作为已批准的实施决定。')
sub('统一术语')
table(['术语','定义'],[['音符 Note','一个有唯一 ID、唯一目标时刻和固定接收位置的点击任务'],['缩圈 Shrink','固定内圆，外环缩小，判定时两者半径相等'],['到位 Arrival','固定接收环，等半径移动圆沿路径到达同一圆心'],['判定目标 Target','玩家需要点击的圆形区域；到位音符点击接收环覆盖区域'],['制谱器 Editor','编辑音乐时间、音符、路径、相机、效果并预览导出的工具'],['谱面 Chart','一首歌某一难度的操作数据与演出数据；不等同音乐资源']], [.25,.75])
p('本文统一使用“制谱器”和“谱面”。首版两种提示方式均为点按音符，不自动引入滑条、拖拽、长按、旋转球或 Game3 的轨道玩法。',True)

page('Game1 玩法表现分析','Game1.mp4  长约 20.16 秒  原始画面 1534 × 866  约 29.99 fps')
refgrid([(ROOT/'tmp/analysis/game1/f021.png','片段约 5.00 s  大外环与固定内圈'),(ROOT/'tmp/analysis/game1/f022.png','片段约 5.25 s  外环接近内圈'),(ROOT/'tmp/analysis/game1/f027.png','片段约 6.50 s  编号与连接线'),(ROOT/'tmp/analysis/game1/f068.png','片段约 16.75 s  多目标同时预读')])
table(['可见证据','可采用的设计'],[['多组外环随时间缩小','外环提供连续时间提示，边缘重合对应目标时刻'],['圆内数字与相邻连接线','给出操作顺序；同拍双押使用同组标识，避免错误排序'],['命中附近出现光环和短残影','短反馈明确一次操作结果，反馈迅速退场'],['有弧形长轮廓、叠圈和背景','属于样片既有表现；本项目首版不照搬其滑条规则']], [.44,.56])
p('分析边界：以全段 0.25 秒抽帧观察整体，并检查 5.00–5.50 秒连续帧。录屏可验证表现趋势，不能反推出输入方式、真实判定窗、具体皮肤代码或评分公式。用户提出的到位音符是本项目明确需求，不能仅凭样片中的移动残影认定它已经展示了该规则。',True)

page('Game3 特效与运镜分析','Game3.mp4  长约 13.74 秒  原始画面 830 × 472  约 31.07 fps')
refgrid([(ROOT/'tmp/analysis/game3/dense_0_00.60.png','约 0.60 s  暗场仍留少量轮廓'),(ROOT/'tmp/analysis/game3/frame_018_04.50.png','约 4.50 s  云雾 微粒与局部光团'),(ROOT/'tmp/analysis/game3/dense_2_08.20.png','约 8.20 s  拉远与场景展开'),(ROOT/'tmp/analysis/game3/dense_2_08.60.png','约 8.60 s  放大并伴强白光')])
table(['观察区间','表现与迁移原则'],[['0.30–0.60 s / 3.00–3.30 s','场景渐暗到近黑。转场可保留，点击目标保护层维持可读'],['4.00–5.75 s','粒子、短残影、经过几何的碎片与旋转。用于背景与退场'],['7.30–8.20 s / 8.40–8.60 s','拉远后突然放大和亮度冲击。拆为尺度与亮度两条轨道'],['12.60 s 附近','再次放大与白光后回落。可转为可调节的节拍强调预设']], [.36,.64])
p('全段按 0.25 秒抽帧，关键转换补充 0.10 秒采样。时间均为片段近似边界。可确认平移与尺度变化，缺少大角度镜头旋转的充分证据；无法确认相机类型、Shader、缓动曲线或音乐同步精度。13.50 秒后播放器 UI 污染画面，不作为主要参考。',True)

page('核心体验与首版范围')
figure(flow,92,'设计图 2  玩家主循环')
sub('体验支柱')
p('两种运动共同训练节奏与空间定位：缩圈读取半径变化，到位读取沿路径靠近的运动。固定接收环让玩家提前知道手指应该落在哪里，移动圆只负责提示时间。每次有效触点对应一次音符判断，视觉演出与音符节奏来自同一歌曲时间。')
table(['系统','10月17日验收版本','后续扩展'],[['游戏','Android教学；两类点击；校准；暂停；结算；本地选曲','iOS构建/实机验收；新玩法；联网'],['演出','玩法/背景强运镜；光效；色彩；粒子；清晰模式','视频背景；复杂3D与高级Shader'],['制谱','独立电脑工具；波形/拍线；音符/路径；动作；预览；导出','高级多轨/批处理；手机制谱；实时录入'],['内容','建议1首完整演示曲2个难度，加独立教学段','长期完善：教学与演示两个曲包各2难度']], [.14,.57,.29])
sub('首版可交付标准')
p('玩家在 Android 实机完成带两种音符与段落演出的歌曲；作者在独立 Windows 制谱器从空白制谱、预览、校验、导出，游戏导入后与预览一致。核心操作离线可用。iOS 源码保留平台抽象与构建配置说明，本次不宣称已产生或验收 iOS 安装包。内容数量与高级工具能力按期限收敛。')
p('歌曲、美术和视频素材需具有项目可用授权；视频作为参考资料，不默认授予其音乐、素材或谱面的再发行权。首版内容数量是制作建议，商业模式、售价和团队规模未确认，不纳入收入预测。',True)

page('两种音符的运动与判定')
figure(mechanics,280,'设计图 3  同一目标时刻的两种视觉提示  两种方式均点击目标区域')
sub('外环收缩')
p('内圆在谱面坐标中的圆心 q 与半径 r 固定，屏幕位置允许随玩法相机强运动。外环初始 R0>r；R(u)=r+(R0-r)×(1-u)，u=1 时边缘重合。默认线性，初始 R0 建议为 r 的2.4倍；圆与环接受同一玩法变换，保持重合含义。')
sub('圆移动到接收环')
p('接收环圆心 q 与半径 r 固定；移动圆半径也为 r，路径 P(u) 在 u=1 时准确等于 q。默认使用直线或三次 Bézier；弧线可由路径工具构造。到位前不穿过接收中心，避免出现第二个“看起来已经重合”的时刻。轨迹淡线与箭头用于预告，不要求玩家拖动手指跟随。')
p('u=clamp((歌曲视觉时间-出现时刻)/(目标时刻-出现时刻),0,1)。目标之后到最晚判定截止前保留稳定轮廓与迟到提示；到达本身不会自动记分。实际结果来自触点时间与位置，而不是碰撞器是否相交。',True)

page('触控判定与输入仲裁')
table(['项目','首轮测试方案'],[['有效输入','每个手指接触的 Began 样本；记录 touchId、startTime、startScreenPosition；按下一次只判一次'],['空间命中','触点落在目标的可点击圆区；到位音符检测接收环覆盖圆区，不检测移动圆'],['时间误差','Δ=校准后的输入歌曲时间-目标歌曲时间；Δ<0 为早，Δ>0 为晚'],['PERFECT','|Δ|≤35 ms；完整反馈'],['GREAT','35 ms<|Δ|≤70 ms；较弱反馈'],['GOOD','70 ms<|Δ|≤100 ms；提示早晚'],['MISS','没有有效输入，且 judgeNow 严格超过目标+100 ms；judgeNow=歌曲时间+输入校准']], [.22,.78])
p('这组窗口是候选配置，应由移动端实测决定。所有边界按包含关系唯一分区；先消费当前输入批次，再扫描到期音符，防止边界帧先判 MISS。手指持续停留、滑过目标、抬起均不产生第二次点击。触点 ID 可复用，以设备 ID、touchId 与接触起始时间组合去重。')
sub('多目标与多押')
p('多押按设备能力支持，不设双押/四押全局上限。inputProfile.requiredTouches声明最低触点数，编译器按同tick音符数核验；设备容量结合平台报告与多触点测试，不把输入槽位数当成硬件能力。复制每根手指Began，用EnhancedTouch而非primaryTouch/底层逐帧轮询。候选按时间+画面空间一对一匹配，先最大化命中数，再最小化|Δ|/距离，最后按ID决胜。')
p('每触点/音符只消费一次，同组音符独立判定。所需触点数超过设备能力时阻止正式开谱并解释，不能静默漏掉手指。接触ID复用、系统手势抢占和超过已验证触点数都纳入设备报告；换设备不会改写谱面。',True)
p('空点默认不扣分、不清连击，也不打游戏命中音。正式候选包不得有重叠且判定窗相交的点击区，以减少乱点与仲裁歧义。若以后设置空点惩罚，应作为新规则版本单独测试。UI 区域触点优先交给暂停按钮，不能同时穿透命中音符。',True)

page('评分 难度与教学')
sub('可解释的成绩')
p('候选准确率为 (1.00×Perfect数+0.75×Great数+0.40×Good数)/总音符数；Miss 权重为 0。分数为 round(1,000,000×准确率)，各平台采用明确的半数向上取整。所有音符等权，多押中每个音符各占一个单位。连击只显示连续命中数，不重复加权分数；Good 继续连击，Miss 清零。')
table(['难度','先控制的空间与节奏','候选预读'],[['入门','单点为主；少量轻运镜；先缩圈后到位；建立定位习惯','900–1400 ms'],['普通','两种方式混合；移动目标；少量多押；变化预告清楚','700–1100 ms'],['进阶','强运镜；曲线路径；多押与短预读；仍保证手指可达','500–850 ms']], [.16,.59,.25])
p('以上区间是测试起点，协议以 leadTicks 保存预读拍长，编译后计算实际毫秒并检查范围。难度不能只靠缩小判定区或增加闪光；同时评估每秒音符数、移动距离/时间、同时预读数量、切换两种提示的频率，以及运镜对寻找目标的负担。')
sub('教学与练习')
p('教学依次介绍固定目标、缩圈重合、移动圆到位、早晚反馈和双押候选。期限内完成自动示范、无失败教学段与重试；制谱预览包含节拍器、自动演奏和 A/B 循环。玩家端细粒度错误段练习、完整错误分布与变速放入后续，不影响首版点击闭环。')
p('期限内结算显示准确率、最高连击、各判定数量；Δ直方图与错误段落导航后续完善。FULL COMBO 仅在 Miss=0 时获得；ALL PERFECT 需要全部 Perfect。练习、自动演奏和编辑预览成绩不写入正式最佳记录。',True)

page('手机演奏界面设计')
figure(gameplay,298,'设计图 4  横屏演奏布局  图内数字和统计仅用于说明功能')
sub('层级与触区')
p('目标的实线轮廓与编号位于最高的游戏保护层；时间提示环其次；路径线在两者后方；装饰与背景更低。顶部显示曲名、进度和暂停；底部集中显示连击、短判定与早晚提示，避免手指盖住信息。HUD 不参与相机缩放或旋转。')
sub('屏幕适配')
p('候选谱面画布为 1280×720。在 Safe Area 内等比 Fit，并居中留边；长屏新增部分只扩展背景，不拉伸圆形。统一保存左下原点的谱面坐标。视觉直径建议不少于约 48 个 UI 逻辑单位，默认半径 52 谱面单位，具体映射与拇指可达范围以实机确认。触区最小尺寸与可见圆尺寸共同预览，不能仅扩大隐形触区解决太小的圆。')
p('横屏已确认。多押目标必须容纳不同手指，编辑器按目标设备模拟最小触区、间隔、边缘与运动轨迹；不锁定具体手指。刘海、圆角、系统手势和暂停按钮纳入安全区。强运镜下目标可以移动，但进入判定窗的目标不能不可见或缩成难以点中的大小。',True)

page('选曲 结算与设置流程')
table(['界面','期限内信息与操作','后续能力'],[['选曲','曲名/作者；时长；难度；最佳；包版本；导入与选择','搜索；收藏；多种排序'],['难度详情','开始；效果模式；校准；是否校验通过','详细难度统计与独立练习页'],['暂停','继续倒数；重试；设置；退出','复杂段落书签'],['结算','准确率；分数；连击；判定计数；重试与选曲','直方图；错误段练习'],['设置','音量；偏移档案；效果与性能档位；触区提示','更多无障碍与触感方案']], [.16,.52,.32])
sub('设备校准')
p('将系统音量、歌曲音量与反馈音效分开。触感反馈默认可关闭；其延迟不会回写到判定。校准分为输入/听觉综合偏移与视觉偏移，先让用户试听并连续点按，再显示早晚分布和建议值。使用中位数等稳健统计，排除明显离群点；不得把偶然一次点击当作设备延迟。')
p('偏移按设备与输出方式保存，例如扬声器、有线耳机和蓝牙；切换音频路线时提示重新选择或测试档案。校准改变的是玩家端对时，不修改曲包的音乐对齐 offsetUs。所有效果档位保持相同音符和判定规则。')
sub('导入体验')
p('使用系统文件选择器导入曲包，复制到应用可写目录，显示标题、资源体积、版本与校验结果。失败时给出字段和定位原因；不可只报“文件错误”。内置曲包在首次体验时无需导入。手机制谱器应复用同一文件服务，避免直接暴露不适用的桌面路径。')

page('特效与运镜的段落设计')
figure(storyboard,260,'设计图 5  演出分镜  背景与玩法相机分轨  两者都允许强运动')
table(['音乐段落','演出候选','操作可读性'],[['引子','冷色雾层；微粒；轻亮度呼吸','单点与长预读建立识别'],['主段','玩法平移；视差；目标随镜头移动','轨迹可预告；触点对应画面姿态'],['高潮','玩法旋转/缩放冲击；光爆；碎片；多押','目标保护轮廓；保证触区与可达性'],['转段','背景渐暗；镜头重定位；配色变化','跳切提前预告，避免同一显示周期瞬移判定目标']], [.15,.45,.40])
p('演出由作者标记音乐段落和事件起点，不用运行时随机频谱直接决定判定区运动。自动频谱可作为背景辅助，正式镜头与关键闪光仍以谱面数据复现。旋转、视差等可作为项目能力扩展，不能说它们已由样片完整证明。')
p('为制谱器提供“节拍脉冲”“场景展开”“休止转场”“高潮碎片”预设；展开后显示每条动作及其目标值。预设是编辑快捷入口，导出结果是可检查的 actions，编译器再生成内部轨道与事件。',True)

page('强运镜下的触控与表现分层')
table(['层/通道','允许的变化','必须保留的规则'],[['背景/装饰','平移/旋转/缩放；云雾；闪光；粒子与碎片','不改变歌曲判定时间'],['玩法层','已允许临判定强运镜移动目标；圆/环/路径共同变换','可逆2D相似变换；scale>0；不把圆变成透视椭圆'],['目标保护样式','边缘与编号随目标移动；轮廓保持对比','保护可读性，不冻结屏幕位置'],['HUD/菜单','固定安全区；独立输入','不随玩法相机变换']], [.20,.45,.35])
sub('按触点对应的画面做空间匹配')
p('时间评分用 inputSong+inputCalibration；理想空间时刻为 rawInputSong+visualCalibration。记录最近约0.5秒渲染帧的 frameId、提交单调时刻、visualSongUs、视口和玩法矩阵；按触点时间减本机显示延迟估计，选择最近先前提交的姿态做反投影。快照已含视觉偏移，不再叠加一次；禁止用回调当前帧矩阵。提交时刻不是实际显示时刻，估计必须靠实机录像/校准验证。')
p('一指按下只匹配当次Began，不允许手指保持后等待目标滑入自动命中。圆和环随共同变换保持重合，镜头不能改 hitTime。玩法相机允许运动与旋转冲击，不设临判定冻结区；改为检查目标出屏、最小尺寸、速度/加速度突变、跳切预告和多指可达性。')
sub('档位不会改变玩法轨迹')
p('清晰模式削减Bloom、粒子、背景运动与非玩法震动，保留玩法轨迹；改玩法运动的辅助模式单独记成绩。记录输入采用的姿态快照与显示估计档案供回放核验。首轮建议持续约100ms严重更新中断进入明确的中断状态，成绩标记异常，可重试或练习继续；阈值实测后冻结，不自动补判或暗中扩大窗口。')

page('音乐时钟与输入时间')
p('音乐、音符、相机、效果和判定共用 SongClock。Unity 官方提供 AudioSettings.dspTime 与 AudioSource.PlayScheduled，可建立基于音频系统的绝对时间起点；这比逐帧累加 deltaTime 更适合节奏游戏，但不代表手机的输出、触控与显示具有零延迟。参见来源 [1]–[4]。')
code('''songNow = songAnchor + (dspNow - dspStart)
dspEvent = dspAnchor + (inputEventTime - realAnchor)
inputSong = songAnchor + (dspEvent - dspStart)
judgedSong = inputSong + inputCalibration
judgeNow = songNow + inputCalibration
visualSong = songNow + visualCalibration
error = judgedSong - noteHitTime''',9)
p('首版速率固定 1.0。dspStart 是预约起点，songAnchor 是本次播放段在音乐文件中的起始秒数。音频准备完毕后留足缓冲再预约；谱面允许歌曲时间为负以显示开头预读。内部 double 秒用于运行，整数微秒用于编译结果与边界比较；只在明确边界进行一次取整。')
sub('桥接与偏移的正负号')
p('EnhancedTouch 的时间是 realtime 时间轴，不能直接减 dspTime。实时采集接近同时的 realAnchor 与 dspAnchor，必要时以前后读取取中点，监测漂移与桥接残差。inputCalibration 为负会把迟到输入向前移；例如目标 5.000 s、输入 5.040 s、校准 -0.040 s，判定误差为 0。visualCalibration 为正让视觉状态提前。')
p('offsetUs 是作品的拍零对齐，玩家偏移属于本地档案。输入系统更新后先消费所有 Began 队列，再以 judgeNow 判超时；deadline=hit+lateWindow 不烘入用户偏移。校准 -40 ms 时，原始输入在目标+120 ms 仍为 +80 ms，有效；不能提前 MISS。暂停、Seek、音频配置变化后重建桥接，实测晚到事件与漂移。',True)

page('Unity 运行架构与共用边界')
figure(architecture,232,'设计图 6  游戏与制谱预览共用的运行核心  连线表示数据依赖')
table(['模块','职责与边界'],[['PackageService','加载曲包、复制资源、版本与哈希校验；屏蔽平台存储差异'],['ChartCompiler','时间映射、合法性验证、路径终点验证、排序与索引；输出不可变编译谱面'],['SongClock / TouchAdapter','音乐绝对时间；realtime-DSP 桥接；复制 Began 样本；不进行视觉特效'],['JudgeEngine','候选搜索、空间检查、触点仲裁、判定状态与成绩；不依赖 Collider'],['TimelineEvaluator','给定绝对歌曲时间求音符、轨道、事件效果状态；支持 Seek'],['RenderPresenter','对象池、圆形材质、背景、Volume、反馈与 HUD；不得回写 hitTime'],['EditorDomain','编辑命令、撤销、草稿、校验与导出；调用同一编译/预览核心']], [.31,.69])
p('Unity 场景建议为启动服务、选曲、演奏、制谱四个入口；核心逻辑位于独立程序集，视觉对象为运行结果。圆/环用解析几何与抗锯齿材质或程序网格，先保证边缘清晰；采用与指定 Editor 可解析的 URP 14.x，使用内置 Volume 后处理，不另混入 Post Processing Stack v2。包版本需锁定并在 c1 实机验证。',True)

page('暂停 跳转 回放与异常恢复')
table(['状态','行为与转移'],[['Loading → Ready','加载、编译、预热；清空输入；检查设备档案'],['Ready → Countdown → Playing','预约音频；展示开头预读；到时进入正式判定'],['Playing → Paused','停止接收游戏触点并冻结运行状态；菜单独立工作'],['Paused → Countdown → Playing','等待手指全部离屏；重建桥接；倒数后恢复'],['Playing → Results','最后音符与判定尾窗完成后结算；保存规则与谱面哈希'],['配置变化/来电/失焦','进入 Interrupted；保留草稿；正式记录标记异常后重试']], [.39,.61])
sub('Seek 必须重建状态')
p('编辑跳转先停止音乐与判定，确定目标歌曲秒数，用 timeSamples 或解码缓存定位音频，清空音符结果和触点，按绝对时间求出背景/相机/参数，再恢复可见音符与事件生命周期，重新预约音频。压缩 AudioSource.time 不能作为可靠的精确跳转真值；不同编码与平台需测试 [6]。')
p('连续参数直接求值；有限事件按 start≤t<end 判断活跃，粒子按事件 seed 与年龄重建或固定步长预热。跳转到事件中间时不重复播放历史音效；自动演奏可直接按目标时刻生成结果。记录作者的拖动预览与真正播放模式，不能把逐帧累加特效当作可 Seek 的实现。')
sub('回放的可复现范围')
p('期限内保存可复核的诊断记录：曲包/谱面哈希、规则、平台与偏移、原始及已校准输入时间、触点屏幕/谱面坐标、接触ID、选中的姿态快照和显示估计档案、结果。相同输入与姿态可确定重算，跨GPU粒子像素完全一致不作保证。完整玩家回放界面后续完善；练习循环每轮独立状态。')

page('移动端实现与性能预算')
table(['项目','候选实现与验收预算'],[['渲染','正交玩法层；URP；背景与后处理单独降档；优先降低全屏透明叠加'],['帧率','首版目标稳定 60 fps；高刷为适配项。目标机实测 P95 帧时≤16.7 ms，P99 与峰值单列'],['分配','演奏稳定阶段托管分配目标为 0 B/帧；预加载与对象池，不每帧生成 JSON 或 LINQ 集合'],['音频','短反馈预加载；歌曲加载策略按内存与解码成本测试；不要所有曲目解压常驻'],['素材','候选曲包上限 200 MiB；解压总量 500 MiB；事件并发上限先以压力谱确定'],['存储','内置资源只读；用户曲包/草稿/缓存存可写目录；提供缓存删除入口'],['发热','连续 20 分钟实机演奏，检查降频、帧时和音画漂移；可自动降装饰档位']], [.22,.78])
sub('平台路径与构建')
p('Android 不直接以桌面 File API 读取包内 StreamingAssets，应通过平台抽象和 UnityWebRequest 访问 [8]。导入使用系统选择器，把授权文件复制到应用目录；不申请无必要的全盘存储权限。iOS 本地最终构建需要 macOS 与 Xcode，Windows 负责 Unity 内容开发不等于能直接产出可安装 iOS 包。')
p('创建最小样例后记录 Editor、URP、Input System、脚本后端与 Android SDK/NDK/JDK，锁定 packages-lock.json。实际包版本以指定 c1 Editor 验证，不用当前官网推荐版本反推。本次以 Android 真机验收，Editor 模拟仅用于快速检查；iOS 源码不等同完成 iPhone 验收。')
p('期限内至少保证一台真实 Android 可持续测试，尽量覆盖第二台不同屏幕或性能档位设备。iPhone/iPad 矩阵和 macOS/Xcode/签名作为 iOS 后续验收前提；当前尚无这些环境。上述性能预算为工程目标，未以假测量填充。',True)

page('制谱器整体布局')
figure(editor,326,'设计图 7  制谱器桌面候选工作区  移动端采用分页与抽屉保留同一数据模型')
sub('期限内工具与完整版边界')
p('本次必须交付无需安装 Unity 的独立电脑制谱器。推荐以同一 Unity 工程构建 Windows x64 Runtime 程序，工具UI、文件操作和预览不得依赖 UnityEditor/EditorWindow。提供舞台、时间轴、属性与动作表，复用游戏编译器/时钟/求值器；首版保留基础完整流程，高级多轨UI与手机工作区后续完善。')
sub('四块工作区')
p('中央舞台支持音符布局、路径控制点、安全区、触区、多押与运动预览；下方时间轴显示波形、拍线、音符、相机、特效和段落标记；左侧工具负责选择与放置；右侧属性显示当前对象的音乐时间、位置、半径、路径和曲线。')
p('在未安装Unity的干净Windows电脑运行导入→制谱→预览→导出才算工具交付通过。电脑预览支持鼠标测试与自动演奏，正式手机多指效果仍需Android测试。运行UI优先采用熟悉的uGUI组件，避免同时维护新的桌面技术栈；原生文件对话框与长路径在10月4日验证 [10]。',True)

page('制谱流程与时间轴操作')
table(['阶段','作者动作','工具提供的反馈'],[['1 建曲包','导入自有音乐、封面，填标题与作者','时长、采样率、资源信息、保存路径'],['2 对时','设置拍零 offset 与 BPM；标记变速/拍号','节拍器与波形对齐；多个段落交叉检查'],['3 先做节奏','在拍线上放点击音符；选择两种表现','自动播放与简洁画面，不先堆效果'],['4 做空间','移动目标；编辑 Arrival 路径；分组多押','终点锁定；触区重叠与可达性提示'],['5 做演出','标段落；添加相机和效果动作','玩法运动轨迹；原始/清晰模式切换'],['6 校验试玩','自动演奏；手动实机；定位错误段','错误列表跳转到对象；早晚与空间 Miss'],['7 导出验证','生成曲包，再由游戏重新导入','资源哈希、版本、校验报告与预览一致性']], [.17,.41,.42])
sub('基本操作能力')
p('基础拍线吸附1/1、1/2、1/4、1/8、1/16与自由tick；三连细分可通过1/12、1/24网格补充。PPQ960可整除多数常用细分，不能表示时提示近似误差。期限内完成选择、复制、删除、拖动、撤销；镜像、复杂批量量化与跨曲模板后续完善。移动目标联动路径终点。')
p('期限内时间轴显示小节/拍/tick 与秒数，支持定位预览和 A/B 循环；BPM 修改默认保持 tick，并提示受影响对象。保持绝对时间再量化、精细拖动预听和高级批量编辑列为后续。BPM修改与基础音符操作仍必须可撤销。')
p('BPM 辅助检测只产生候选，作者用节拍器确认后写入谱面。不会承诺从音乐自动产出可玩的谱面；节奏密度、双手可达性和两类表现的分配仍需要作者决定。',True)

page('编辑命令 事件工具与诊断')
sub('可靠编辑')
p('所有编辑通过命令事务修改文档模型，记录稳定对象 ID 和修改前后状态；拖动连续动作合并成一次撤销。撤销/重做、剪贴板和模板都不直接修改运行中的场景对象。保存采用临时文件加原子替换，周期自动保存到恢复草稿，移动端切到后台立即保存未提交修改。')
table(['工具','必要能力'],[['路径工具','直线与三次 Bézier；控制点；预览进度；末点与 target 联动；提前到位诊断'],['相机工具','背景/玩法动作分离；目标值；曲线；强运动预览；重叠冲突提示'],['效果工具','参数注册表；预设展开；强度包络；生命周期；随机种子；并发计数'],['时间诊断','音符 hit/appearance 时间；输入Δ；late deadline；BPM分段；偏移来源'],['空间诊断','触区可见；边缘距离；多押重叠；临判定目标运动；宽屏和SafeArea模拟'],['资源诊断','缺失文件；大小/哈希；不支持的类型；包版本与授权备注']], [.22,.78])
sub('从错误直接定位')
p('错误格式为严重级别、代码、对象 ID、JSON 路径、音乐时间和解释。点击错误可定位时间轴与舞台对象；修改后增量复查，导出前跑完整校验。语法/资源/时序矛盾为阻断错误，演出密度或低对比为警告，只有用户明确接受可发布的警告才导出。')
p('“自动演奏通过”只说明目标事件可运行，不能证明真实手指点得准。导出曲包要经过手动实机测试。作品署名、歌曲来源与授权备注可以放 metadata；这些记录用于作者管理，不冒充版权授权证明。',True)

page('ADOFAI 文件结构核验','先核查已有格式 再定义本项目作者协议  证据分级 A 官方 B 文件样本 C 社区工具源码')
p('官方编辑器公告确认 .adofai 文件可与同目录音乐配套分享；官方说明和更新记录能解释部分事件行为。本次没有找到完整的、版本化官方 JSON Schema，因此下表的字段结构来自公开文件与独立社区工具交叉核验，不能称为当前全部官方协议。来源 [A1]–[A8]。')
table(['分区/字段','可核验含义','证据边界'],[['settings','歌曲/关卡设置与默认表现参数','旧样本/社区模型[A4][A5]，不套用所有现代默认值'],['angleData / pathData','角度数组或字符路径，描述轨道方向','社区转换与公开旧样本[A4][A6]；不是等间距音符时间'],['actions','带类型与位置锚点的事件序列','floor/eventType由样本与源码核验[A4][A5]'],['decorations','装饰对象数据与动作序列分区','社区模型/生成器支持[A5][A8]；旧样本无该分区'],['floor / eventType','砖块位置锚点与事件类别','[A4][A5]；floor不等于固定beat或毫秒'],['angleOffset / duration / ease','事件偏移、持续参数与缓动','[A1][A7]；具体单位/特殊事件按版本核验'],['tag / eventTag','装饰选择标签与事件引用标签','生成器参数流支持用途区分[A8]']], [.27,.40,.33])
sub('实际可借鉴的组织方式')
p('先声明关卡初态和装饰对象，再描述对相机、背景、对象、粒子等的动作；由类型化事件支撑制谱器表单。音频/图片使用资源引用，作者文件与运行计算结构分离。相机和效果可以批处理、设缓动与复用预设，而不必手写每一帧状态。')
p('研究使用官方公告、官方机制页、公开上传谱面与作者自行编写的工具源码，没有使用反编译游戏实现。公开样本只证明该文件的字段，未证明其音乐、美术或谱面的再发行授权。',True)

page('ADOFAI 时间与事件语义')
sub('砖块与角偏移不能直接当作拍点')
p('官方判定窗口文档的基础换算为180°对应1 beat，固定BPM下角度时间量为 angle/BPM×1000/3 毫秒 [A2]。官方旧公告解释角偏移可使事件在击中tile之后执行 [A1]。本计划书受限推导：常规两球、固定BPM、无暂停及特殊行为时，90°在120 BPM下相当于250ms；这不是覆盖所有事件的导入算法。')
code('''{
  "floor": 12,
  "eventType": "MoveCamera",
  "angleOffset": 90,
  "duration": 1,
  "rotation": 15,
  "zoom": 100,
  "ease": "Linear",
  "eventTag": "chorus"
}''',9)
p('以上是根据已核验字段重写的结构示意，不是完整可导入关卡。duration=1 的单位不能据此认定为1秒；zoom、rotation等也必须结合ADOFAI自己的单位和参照。路径角度、变速、暂停、多球和特殊砖块会改变 floor 对应时间，不能使用 floor×60/BPM 作为通用时刻。',True)
table(['观察到的能力','本项目采用方式'],[['Move Camera 平移/旋转/缩放[A1][A7]','保留明确目标层的MoveCamera，可强运动玩法目标'],['装饰声明与 MoveDecorations[A5][A8]','先定义带ID的decorations；本项目名称为MoveDecoration'],['对象标签 / 事件标签[A8]','对象批选与动作批处理独立，不把同名tag混为一类'],['RepeatEvents[A8]','借鉴重复预设流程；本期在编辑器展开普通动作'],['版本与特殊时长修正[A3]','保留schemaVersion、明确单位与迁移；不假定duration语义相同']], [.39,.61])
p('官方v2.9.7更新提及特殊砖块beat计算、FreeRoam duration以及版本迁移问题 [A3]。本期借鉴数据组织，不实现 .adofai 直接导入或轨道节奏计算，也不移植其玩法。',True)

page('从 ADOFAI 映射到本项目')
table(['ADOFAI 思路','本项目唯一作者数据','原因'],[['关卡settings','settings 默认镜头/背景/发光','初态只保存一份'],['路径表达轨道方向','notes 明确目标tick，paths只存几何','触屏节奏不依赖轨道角度'],['floor+angleOffset','tick 与 durationTicks','单位明确；跨BPM通过分段时间积分'],['decorations','稳定id、初态、layer、tags、素材引用','装饰不冒充点击音符'],['actions+eventType','动作白名单、目标引用、to、ease、seed','编辑表单可生成，导出可校验'],['标签与重复事件','编辑器批选/重复后展开具体ID与动作','运行时无需递归或动态查标签'],['事件动画','编译成内部轨道与有限事件表','作者不同时手写tracks/events造成多份真值']], [.25,.41,.34])
def protocolfig(w,h):
    bw=(w-24)/3
    box(0,h-90,bw,77,'作者协议','settings / notes / paths\ndecorations / actions')
    box(bw+12,h-90,bw,77,'校验与编译','单位 / 引用 / 冲突\ntick → us  动作 → 轨道')
    box(2*(bw+12),h-90,bw,77,'运行与工具预览','共同SongClock\n同一确定性求值器')
    arrow(bw+2,h-50,bw+10,h-50);arrow(2*bw+14,h-50,2*bw+22,h-50)
figure(protocolfig,102,'设计图 9  参考结构后的作者协议与内部运行数据边界')
p('作者格式以 settings/decorations/actions 组织演出，原本可手写的 tracks/events 改为编译器内部产物；两种格式不同时成为作者输入。音符时间、路径、相机动作共享PPQ960与offsetUs。noteId+相对拍偏移可以用于编辑工具，但导出统一解算成tick。')
p('本项目字段名称与取值以本计划书为准。使用相似概念和部分相同事件名称，不代表 .adofai 文件兼容，也不宣称复刻ADOFAI的事件执行顺序。首版避免运行时脚本、递归重复与隐式条件事件，便于AI实现和15个日历日内集成验证。',True)

page('曲包结构与协议版本')
code('''demo-ring.ringpack                  (ZIP container)
  manifest.json                    (package metadata)
  charts/normal.chart.json          (one difficulty)
  charts/easy.chart.json            (optional difficulty)
  audio/song.wav                   (music asset)
  images/cover.png                 (optional cover)
  licenses/credits.txt              (optional credits)''',9)
table(['字段组','规范约定'],[['编码与容器','UTF-8 JSON；无注释、无尾逗号；ZIP 普通文件；路径统一 /；数字必须有限'],['身份','packageId 稳定；packageRevision 递增；chartId 稳定；chartRevision 递增'],['版本','formatVersion 管曲包；schemaVersion 管谱面；ruleVersion 管判定/评分；各自独立'],['能力','requiredCapabilities 声明必要音符、路径和轨道能力；缺少必要能力即拒绝'],['资源','assetId 引用相对路径；保存 bytes、sha256 与 mediaType；音乐附采样率/时长'],['校验','每个资源和谱面按原始文件字节 SHA-256 校验；哈希证明完整性，不证明可信作者'],['存储','导入复制到应用目录；草稿/缓存/成绩不写回正式曲包']], [.21,.79])
p('主版本未知即拒绝，支持的小版本新增能力由 capability 决定是否可读；补丁版本不改变字段语义。迁移创建副本并保留源文件，不静默覆盖作品。ID 用区分大小写的 ASCII 标识，建议 [A-Za-z0-9._-]，1–64 字符。导出前固定对象顺序与数字格式便于差异审查；导入不依赖键的顺序。')
p('安全约束：拒绝绝对路径、盘符、..、符号链接和重复归一化文件名，防止解压越界；限文件数、单文件大小与解压总量。首版只允许白名单音频/图像与内置效果预设，不加载曲包里的脚本、程序集、Shader 或任意外部 URL。资源上限见第16页，压缩比例与并发数量通过压力测试再冻结。',True)

page('音乐时间 坐标与变速映射')
sub('tick 与拍零')
p('ticksPerQuarter 固定为 960，tick 是四分音符拍长的整数单位。音符目标 tick≥0；预读开始可以为负。offsetUs 表示 tick=0 在音频文件中的微秒位置，正值表示音乐先播放后到拍零；负值表示拍零在音频开始之前。首个音符的真实目标时间必须≥0，开场可预读但不能要求点击尚未开始的音乐。')
code('''T(tick) = offsetUs + sum(segmentTicks * 60000000 / (BPM * 960))
hitUs = roundHalfUp(T(hitTick))
appearUs = roundHalfUp(T(hitTick - leadTicks))
u = clamp((visualUs - appearUs) / (hitUs - appearUs), 0, 1)''',8.7)
p('tempos 必须包含 tick=0、BPM>0 且严格升序；每段左闭右开，在变化点采用新 BPM。负 tick 用第一段 BPM 向前外推，积分在 double 高精度值上进行，不逐拍取整。meters 只影响小节标尺，不影响音乐时长；拍号变更首版限定在前一拍号的小节边界。BPM 是每分钟四分音符拍数。')
table(['例子  offsetUs=500000','计算结果'],[['0–7680 tick 为 120 BPM','0 tick=0.500 s；1920=1.500 s；3840=2.500 s；5760=3.500 s'],['7680 tick 开始为 150 BPM','7680=4.500 s；8640=4.900 s'],['8640 的 leadTicks=1920','出现 tick=6720 →4.000 s；预读实际长 900 ms'],['时间显示','4/4 每小节=3840 tick；6/8 每小节=2880 tick；编辑器显示拍号分母对应拍单位']], [.42,.58])
sub('几何空间')
p('谱面坐标 1280×720，左下原点，x 向右、y 向上；半径同一单位。玩法相似变换先以画布中心旋转/缩放，再平移，再 Safe Area 等比 Fit。触点反变换按同一顺序逆运算，UI 命中先于谱面命中。整数 tick 对时，浮点坐标布局；禁止同时保存另一套可冲突的 hitSeconds。',True)

page('音符与路径字段规范')
table(['字段','类型与要求'],[['id / type','唯一 ID；type 首版仅 tap；其他玩法必须新能力'],['tick / leadTicks','非负整数目标 tick；正整数预读拍长；可编译到负出现时间'],['motion','shrink 或 arrival；只改变表现，不改变点击规则'],['target','长度 2 的有限数数组 [x,y]；为固定接收中心'],['radius','有限正数；默认 52；不能由镜头或皮肤修改判定时序'],['groupId','可选；同拍组只接受同tick；数量不得超过谱面requiredTouches'],['shrink','motion=shrink 时必需；startScale>1；进度默认线性'],['pathId','motion=arrival 时必需；引用已有路径；与 shrink 参数互斥'],['sequence','可选非负显示序号；未填由排序生成；多押同组显示共享符号'],['hitSoundAssetId','可选白名单短音效引用；未填用内置反馈']], [.28,.72])
sub('路径对象')
p('路径字段为 id、kind 和 points。kind=line 时 points 恰有两点；kind=bezier3 时恰有四点 P0、P1、P2、P3。末点必须与音符 target 在误差≤0.01 谱面单位内一致；为多音符复用路径时各 target 必须一致，否则需复制路径。编辑器移动目标应连动末点，不能导出断开的终点。')
code('''Line(u) = (1-u) * P0 + u * P1
Bezier3(u) = (1-u)^3 * P0 + 3*(1-u)^2*u*P1
             + 3*(1-u)*u^2*P2 + u^3*P3''',8.8)
p('到位圆默认按参数 u 求路径位置，作者可以看到非匀速路径的时间分布；不默认按弧长匀速。若未来引入弧长参数化，必须新增 motion 曲线能力并改变预览器/编译器一致实现。到位前的任何 u<1 不得使中心已等于 target；相切并非判定，相同圆心和相同半径才是边缘完全重合。')
p('v1 不允许音符目标本身有独立移动轨道；只有一致的玩法层变换可改变其屏幕位置。首版路径经解析端点检查与自适应采样检查，实际相同中心的提前根需可靠诊断，不能只查最后一个控制点。',True)

page('settings decorations 与 actions 规范')
table(['结构','必需字段与规则'],[['settings','scene/gameplay初始position=[x,y]、rotationDeg、zoom；background的opacity/tint；bloom.intensity'],['decorations[]','id；type=shape或image；shape或assetId互斥；layer=scene/decor；初态position/rotationDeg/scale/opacity；可选tags'],['actions[] 公共字段','id、eventType、tick；持续动作durationTicks≥0、ease；类型决定targetId/targetIds、to与效果参数'],['inputProfile','mode=touch；requiredTouches为正整数；同tick音符数与同拍组均校验，不硬编码2/4上限']], [.24,.76])
table(['eventType','v1 白名单语义'],[['MoveCamera','targetId=scene或gameplay；to中的position/rotationDeg/zoom，zoom>0；允许临判定运动'],['MoveDecoration','targetIds引用装饰ID；to为position/rotationDeg/scale/opacity；scale>0'],['SetBackground / SetBloom','to：opacity为[0,1]标量、tint为3个[0,1]分量的线性RGB数组；或intensity≥0（候选上限2）'],['ParticleBurst','durationTicks>0、layer、position、builtin preset、count、seed；不改变判定'],['LightPulse','durationTicks>0、layer=background、strength∈[0,1]；衰减包络与seed']], [.31,.69])
p('tick为非负整数，durationTicks为非负整数；ease=linear/smoothstep/step，按歌曲时间插值，step在结束点跳变。时长0为瞬时赋值；正值为T(tick+durationTicks)-T(tick)。从起点先前状态到to，结束保持末值；同目标/属性动画不重叠，前一结束与后一开始可相接；相同起点写同属性拒绝。未写属性保持原值。',True)
p('decor与scene装饰跟随scene相机，玩法只跟随gameplay相机；粒子位置为所在层谱面坐标，count>0、seed为整数，预设限内置。装饰标签批选/重复在工具展开具体ID/actions。LightPulse增量strength×(1-u)²相加后钳制；粒子按seed/年龄重建，有限效果不回写基础轨道，Seek不靠历史累加。',True)

page('完整曲包示例 清单','下列为合法 JSON 结构示例  示例资源名与哈希用于说明  实际曲包由导出器填入文件真实信息')
code('''{
  "formatVersion": "1.0.0",
  "packageId": "demo-ring-001",
  "packageRevision": 1,
  "title": "Ring Demo",
  "artist": "Example Artist",
  "minRuntimeVersion": "1.0.0",
  "requiredCapabilities": [
    "tap.v1", "touch-multi.v1", "motion.shrink.v1",
    "motion.arrival.v1", "path.bezier3.v1", "automation.v1"
  ],
  "audioAssetId": "audio.main",
  "assets": [{
    "id": "audio.main",
    "path": "audio/song.wav",
    "mediaType": "audio/wav",
    "bytes": 5760044,
    "sha256": "1111111111111111111111111111111111111111111111111111111111111111",
    "sampleRate": 48000,
    "channels": 2,
    "durationUs": 30000000
  }],
  "charts": [{
    "id": "normal-001",
    "path": "charts/normal.chart.json",
    "sha256": "2222222222222222222222222222222222222222222222222222222222222222"
  }]
}''',8.3)
p('示例 WAV 时长 30 秒、48 kHz、双声道、16 位 PCM 的媒体数据量为 5,760,000 字节，容器头大小由实际文件决定；示例 bytes 含常见 44 字节头，不作为统一 WAV 头长度规定。哈希 111…和 222…为示意值，不能用于运行包完整性检查。导出器读取实文件生成 bytes、时长和 SHA-256。',True)
p('谱面示例在下两页连续展示，合并后为一个完整顶层JSON对象；含5个音符、2条路径、1个双押示例组、变速、初态、1个装饰与4个动作。requiredTouches=2仅表示该示例最低需求，协议支持设备能力内更多触点。',True)

page('完整谱面示例 时间 路径与音符','normal.chart.json  上半部分  与下一页接续合并')
code('''{
  "schemaVersion": "1.0.0", "chartId": "normal-001",
  "chartRevision": 1, "ruleVersion": "tap-mobile-1",
  "author": "Example Mapper",
  "difficulty": {"name": "Normal", "rating": 6},
  "inputProfile": {"mode": "touch", "requiredTouches": 2},
  "canvas": {"width": 1280, "height": 720, "origin": "bottomLeft"},
  "timebase": {
    "ticksPerQuarter": 960, "offsetUs": 500000,
    "tempos": [{"tick": 0, "bpm": 120}, {"tick": 7680, "bpm": 150}],
    "meters": [{"tick": 0, "numerator": 4, "denominator": 4}]
  },
  "paths": [
    {"id": "p01", "kind": "bezier3",
     "points": [[440,150],[600,100],[720,450],[850,360]]},
    {"id": "p02", "kind": "bezier3",
     "points": [[1000,500],[1100,480],[960,500],[930,400]]}
  ],
  "notes": [
    {"id": "n001", "type": "tap", "motion": "shrink",
     "tick": 1920, "leadTicks": 1920, "target": [330,360],
     "radius": 52, "shrink": {"startScale": 2.4}},
    {"id": "n002", "type": "tap", "motion": "arrival",
     "tick": 3840, "leadTicks": 1920, "target": [850,360],
     "radius": 52, "pathId": "p01"},
    {"id": "n003", "type": "tap", "motion": "shrink",
     "tick": 5760, "leadTicks": 1920, "target": [350,280],
     "radius": 52, "groupId": "g01", "shrink": {"startScale": 2.4}},
    {"id": "n004", "type": "tap", "motion": "arrival",
     "tick": 5760, "leadTicks": 1920, "target": [930,400],
     "radius": 52, "groupId": "g01", "pathId": "p02"},
    {"id": "n005", "type": "tap", "motion": "shrink",
     "tick": 8640, "leadTicks": 1920, "target": [620,340],
     "radius": 52, "shrink": {"startScale": 2.4}}
  ],''',8.1)
p('示例的路径末点分别等于 n002 和 n004 的 target。g01 的两个目标同拍且彼此分开；n005 的预读跨过 120→150 BPM 分界，应按变速映射计算 900 ms，而不是直接拿最后 BPM 计算整段。',True)

page('完整谱面示例 相机与特效','normal.chart.json  下半部分  从上一页 notes 数组之后继续')
code('''  "settings": {
    "scene": {"position": [0,0], "rotationDeg": 0, "zoom": 1},
    "gameplay": {"position": [0,0], "rotationDeg": 0, "zoom": 1},
    "background": {"opacity": 1, "tint": [0.08,0.12,0.2]},
    "bloom": {"intensity": 0.2}
  },
  "decorations": [{
    "id": "d01", "type": "shape", "shape": "builtin.ring",
    "layer": "decor", "position": [640,360], "rotationDeg": 0,
    "scale": 1, "opacity": 0.35, "tags": ["chorus"]
  }],
  "actions": [
    {"id": "a01", "eventType": "MoveCamera", "targetId": "gameplay",
     "tick": 1920, "durationTicks": 3840, "ease": "smoothstep",
     "to": {"position": [30,0], "rotationDeg": 8, "zoom": 1.05}},
    {"id": "a02", "eventType": "MoveDecoration", "targetIds": ["d01"],
     "tick": 1920, "durationTicks": 3840, "ease": "linear",
     "to": {"rotationDeg": 90}},
    {"id": "a03", "eventType": "ParticleBurst", "tick": 5760,
     "durationTicks": 960, "layer": "decor", "position": [640,360],
     "preset": "builtin.shards", "count": 24, "seed": 17},
    {"id": "a04", "eventType": "LightPulse", "tick": 7680,
     "durationTicks": 480, "layer": "background", "strength": 0.25,
     "seed": 0}
  ]
}''',7.9)
sub('示例预期行为')
table(['歌曲时间','预期动作'],[['0.500 s','n001开始预读；相机采用settings初态'],['1.500 s','n001边缘重合；n002预读；玩法相机与装饰动作开始'],['2.500 s','n002到位；g01开始预读；玩法目标继续随相机移动'],['3.500 s','g01重合；相机达到目标值并保持；碎片开始'],['4.500 s','切换150 BPM；背景脉冲开始，持续200ms'],['4.900 s','n005缩圈重合；音乐可继续播放']], [.21,.79])
p('本例a01明确移动玩法目标，动作期间仍可点击；命中按对应画面姿态做空间检查，时间等级按输入误差。a03/a04只影响演出。实际结算同时考虑音乐结束策略和最后判定尾窗，不由最后一条粒子决定。',True)

page('编译结果 校验与迁移')
table(['错误码','导出或加载的处理'],[['E_VERSION / E_CAPABILITY','主版本或必要能力不支持；阻断并解释所需能力'],['E_RESOURCE / E_PATH','缺文件、大小/哈希不符、越界路径；阻断'],['E_ID / E_FIELD','重复ID、未知类型、缺必填、非有限值、互斥字段并存；阻断'],['E_TIME / E_TEMPO','负目标时间、BPM≤0、重复变速tick、空预读、目标晚于音乐；阻断'],['E_GEOMETRY','路径终点不匹配、提前重合、目标不可见、相交窗口触区重叠；阻断'],['E_GROUP / E_TRANSFORM','组内tick不同、触点声明不足、不可逆/非等比变换；阻断'],['E_ACTION / E_REFERENCE','目标不存在、同属性动作区间冲突、非法类型/单位；阻断'],['W_DENSITY / W_EFFECT','预读拥挤、突变镜头、过度闪光、粒子并发高；警告与定位']], [.39,.61])
sub('发布校验规则')
p('v1采用固定白名单字段与类型，未知核心字段阻断并指向版本；未来extensions须有命名空间/能力声明，不能默默忽略。JSON解析成功不是谱面合格。JsonUtility的Dictionary与裸数组限制[9]需DTO或严格字段解析器；所选库本期在Android IL2CPP验证，iOS IL2CPP在后续构建阶段验证。')
p('ChartCompiler输出按hitUs/ID排序的音符数组，含appearUs、hitUs、deadlineUs、target/radius、motion/pathIndex/groupIndex；将settings与actions编译成内部属性轨道和有限事件区间，再生成索引。运行结果只由作品与规则版本生成，不含玩家偏移；作者格式不接收另一套tracks/events。')
sub('迁移与兼容验证')
p('迁移步骤为读原版本→验证→显式转换→验证新版本→预览对比→另存；不得只改版本字符串。相同 tick/位置/资源应保持含义，无法自动迁移的玩法或曲线明确报错。回归样例至少覆盖变速、负预读、双押、路径、相机、Seek、缺资源和未知能力。后续录入协议版本独立于曲包版本。')

page('平板触控实时录入路线','已确认的后续能力  电脑运行制谱器  平板连接电脑采集触控')
def recorderfig(w,h):
    box(0,h-115,150,100,'电脑制谱主机','音乐播放与 SongClock\n录入会话与草稿\n量化  撤销  导出')
    box(w-150,h-115,150,100,'平板采集端','采集区与预览\n多点触控原始时间\n连接状态与录入提示')
    arrow(158,h-45,w-158,h-45);txt(w/2,h-31,'时间同步  视口与歌曲快照',8,BLUE,center=True)
    arrow(w-158,h-91,158,h-91);txt(w/2,h-109,'触点样本  序号  确认与补传',8,BLUE,center=True)
    txt(w/2,8,'实时录入先产生可撤销草稿  不直接写入已发布谱面',9,BLUE,True,True)
figure(recorderfig,148,'设计图 8  电脑与平板的录入会话')
sub('推荐的第一种工作方式')
p('电脑是音乐与谱面的主时钟，平板显示采集画布、倒数与录入状态。作者一边听电脑播放，一边在平板上点按。采集端保存每次 Began 的本机单调时间和位置，主机转换为歌曲时间与谱面坐标；记录期间不强制量化。平板预览与触点来自同一坐标画布，网络状态不稳定时给出明显提示。')
p('一轮录入后形成 Take 草稿：保留原始微秒、原始屏幕坐标、映射后坐标、校准、同步质量和接触 ID；作者再决定量化网格、双押合并、缩圈/到位类型与路径。两手同时点按可形成同拍候选，但不会自动把接近时刻的两个触点压成同一 tick。')
table(['阶段','能力范围'],[['首个后续版本','电脑播放 + 平板点击；局域网配对；录入开始/停止/补录；原始草稿；量化撤销'],['再扩展','选择空白采集区或同步谱面预览；双押辅助；节奏刷与位置刷分离'],['后续验证项','USB 有线连接；平板本地音频同步；更复杂输入。Android 与 iPad 的接入分别验证']], [.25,.75])
p('候选先用局域网连接，USB 作为同协议的后续传输；有线不等于零延迟，也不能默认 Android 与 iPad 都可用同一 USB API。若希望音乐在平板播放，则必须另建音频启动与漂移同步机制，不能沿用“电脑唯一音频源”的简单方案。',True)

page('触控录入连接与采集协议')
table(['消息类型','最小字段与用途'],[['Hello / Pair','protocolVersion、deviceId、sessionId、配对令牌、supportedTouches、canvasVersion；本地显示短码确认'],['ClockPing / ClockPong','seq、PC_send、tablet_receive、tablet_send、PC_receive；全为本机 monotonicUs'],['SongSnapshot','generation、transportState、songUs、pcMonoUs、chartHash、canvasVersion、transformVersion'],['CanvasState','视口像素矩形、屏幕方向、画布尺寸/原点、实际变换矩阵、版本和生效时间边界'],['RecordBegin / End','takeId、generation、有效歌曲区间、倒数边界；开始由主机确认'],['TouchBatch','sessionId、takeId、generation、seq；samples[] 含 sampleIndex、touchId、startMonoUs、x/y、phase、已显示的画布/变换版本'],['Ack / Resend / Abort','同一会话代次的连续批次序号；缺口；结束原因；先保存样本再确认']], [.27,.73])
p('transport 可先用可靠有序局域网连接，例如 WebSocket；协议不绑定某个 Unity 网络库，相关客户端、移动权限和 IL2CPP 支持需试验。消息限定大小、字段和值域，配对令牌仅限当前本地会话；首版无需公网云账号。后续传输可使用网络式 USB 通道或平台桥接，不承诺跨平台同实现。')
sub('同步与迟到样本')
p('用多轮往返样本估算两台设备单调时钟的偏移与漂移，优先低 RTT 样本并记录不确定性，再把 tablet startMonoUs 映射到 PC 单调钟与 SongClock。接收时刻只用于质量统计，不用作音符时间。链路不对称需校准。TouchBatch x/y 是有效采集画布归一化坐标 [0,1]，左下原点，留边不参与录入；Take 另留原始像素与视口尺寸。')
p('每次开始、Seek、恢复或重连改变 generation。旧代样本不能进当前 Take，但可通过历史补传回填其原 Take 的有效区间。采集画布建议固定；变换版本必须对应平板已显示的 CanvasState，不能按主机最新状态反投影。迟到样本不伪装成已实时显示。')
p('seq 是 (sessionId,takeId,generation) 内从0递增的批次序号；重传保留原序号。去重键为上述三字段+seq+sampleIndex，ACK只确认同域最大连续批次。映射模型为 pcMonoUs=a×tabletMonoUs+b；Take 保留 clockModelId、a、b、uncertaintyUs 与当次结果，再按所属 generation 的歌曲锚点换算。同步更新不改已有草稿，重算必须可撤销。',True)
p('验收要比较参考本地输入与远端输入的时间误差分布，在有网络拥塞、乱序/重复、断线、Seek、不同刷新率时核验。质量阈值在原型实测后冻结；不把“网络往返短”直接等同“录入准确”。',True)

page('验证矩阵与可接受结果')
table(['测试面','必要场景','通过标准'],[['时序','全部窗口边界；10分钟长曲；暂停/Seek；偏移正负','相同输入/姿态样本得相同判定；无重复/漏消费；记录漂移'],['几何','两种终点；快速平移/旋转/缩放；SafeArea；触区边界','目标时刻重合；对应画面投影一致；目标可点'],['多点','2/3/5指及设备实测上限；短触；ID复用；滑过；UI','一触点一音符；全部已支持触点独立处理；不持指自动判'],['编辑','撤销/重做；BPM改动；复制；恢复；导出重导入','作品语义不变；工具与手机时间/路径/动作一致'],['演出','任意Seek；清晰模式；暗场/强闪；并发压力','可还原；清晰模式保留玩法轨迹；目标可读'],['当前平台','Android20分钟发热；音频切换/后台；干净Windows','Android达到预算或降装饰；电脑无需Unity完成制谱'],['后续录入','重复/迟到/断线；代次变化；时钟漂移','原始样本持久化再ACK；不跨代混入；可撤销']], [.14,.40,.46])
sub('玩法与内容的人工验收')
p('新手与有音游经验的玩家分别测试教学、单点、到位、多押和强运镜。记录定位错误、早晚偏向、手指遮挡、镜头干扰与音频路线；分开诊断读谱、空间可达性和输入延迟。35/70/100ms窗口及48逻辑单位触区保持候选，原型验收后冻结。')
p('同一输入和记录姿态以30/60/120Hz处理频率回放，应保持匹配与等级一致；数学投影误差候选≤2px，不等于已知屏幕显示延迟。注入100–200ms卡顿检查明确中断，无静默批量Miss。性能用设备日志/Profiler，表现用录像/Seek，自动演奏不能代替手指体验。',True)

page('10月17日交付与验收约定','2026年10月3日至17日共15个日历日  最终截止2026-10-17 23:59  Asia/Shanghai')
table(['交付项','可验收产物与完成条件'],[['Android游戏','APK；横屏多触点；两类音符；强运镜移动目标；校准/暂停/结算；本地曲包'],['独立电脑制谱器','Windows x64 EXE及全部Runtime依赖/数据目录；无需Unity；空白→保存→预览→导出闭环'],['内容与演出','建议1首完整合法演示曲、2个难度、独立教学段；平移/旋转/缩放、色彩、粒子预设'],['统一数据','v1协议说明、示例、错误报告；游戏/工具共用编译器与求值器；导入导出往返一致'],['完整工程源码','指定Unity版本、锁包、构建入口、素材/授权说明；Android与Windows可从干净目录重建'],['iOS本次边界','交源码工程与待验证清单；当前无Mac/苹果设备，不把源码称为已验收iOS构建'],['验收记录','目标Android型号/系统/实测触点数、版本哈希、已知问题、实机与干净电脑流程记录']], [.25,.75])
sub('截止管理与可减范围')
p('这是压缩交付计划，AI并行提高产出速度，最终仍以可运行构建和真人体验判断完成。10月4日若Android或独立EXE尚不能启动，立即优先修复工具链；10月9日必须形成工具→曲包→手机的闭环；10月12日冻结功能，最后5天留给回归与发布。')
p('允许先减少额外歌曲、视频背景、复杂Shader、装饰数量和高级工具UI；不削减独立EXE、两种音符、设备能力内多押、强运镜移动目标、可复现判定和基础制谱流程。平板实时录入、iOS构建/真机和上架审核在后续排期，源码保留扩展接口。')
p('人每天4–8小时以上投入：阅读AI变更、听节奏、触屏测试、内容确认与集成验收。无法通过硬门槛时记录具体阻塞与修改后的范围/预估；不得把未验收模块写成已完工，也不凭AI产出数量宣称进度。',True)

page('每日制作计划 10月3日至10日')
table(['日期','当天可运行产物','退出检查'],[['10/03','冻结字段/输入/交付；建立Android游戏与Windows工具入口，共用Runtime程序集','接口契约与文件归属；指定Editor能开工程'],['10/04','触控测试APK与独立EXE均启动；记录工具链与资源导入样例','真AndroidBegan采集；干净电脑无需Unity；基础文件对话框'],['10/05','两种音符+玩法强运镜样段；渲染姿态历史与事件时间桥接','平移/旋转/缩放对应画面点击；多指逐一保留'],['10/06','60秒段可完成：判定/校准/暂停/结算，强运动与多押','边界/短触/ID复用/掉帧；诊断记录可重算'],['10/07','曲包服务、DTO、时间映射、引用与动作校验；示例可加载','跨BPM预读/动作时长；错误定位；哈希'],['10/08','独立EXE内音频波形/拍线/BPM、两类音符/路径、保存重开、撤销和预览','用工具创建60秒样谱；撤销可还原'],['10/09','EXE从空白导出，Android重新导入演奏','同谱时间/路径/移动目标一致；形成首个闭环'],['10/10','EXE相机/效果动作表与预设；任意Seek重建','玩法层强运动；装饰种子与效果生命周期一致']], [.11,.52,.37])
sub('并行依赖顺序')
p('协议与接口冻结后，输入判定、制谱器、演出可并行；每个分支交付小块可运行产物。工具端在完整玩法尚未完成时用相同编译器的自动演奏预览，游戏端用固定示例驱动。10月9日前不能只分别演示两个程序，必须实际导出并重新导入。')
p('相机/效果表单优先支持白名单动作，不先投入完整曲线编辑器；波形可预计算缩略缓存，时间轴只绘可见范围；先用内置简单几何材质与有限预设，以稳定可运行构建支撑之后内容制作。',True)

page('每日制作计划 10月11日至17日')
table(['日期','当天交付与回归','退出检查'],[['10/11','完整演示曲2难度、教学段；选曲/设置/校准；清晰与性能档位','新手能理解两类提示；内容由人工试听与触屏确认'],['10/12','功能冻结；强运镜多押、偏移、暂停、Seek、协议往返全回归','只保留缺陷修复；禁止新玩法/新平台库'],['10/13','Android连续20分钟；发热/后台/路线切换；不同触点需求','记录帧时和漂移；实际支持触点数明确；异常不暗中计分'],['10/14','干净Windows完成独立工具全流程；Android实机试玩；RC1','无需Unity/开发环境；资源/保存/导出正常'],['10/15','干净目录恢复源码并构建Android与Windows；RC2','锁包/工具链/构建说明可复现；排除本机隐含依赖'],['10/16','只修阻塞与严重缺陷；冻结包/哈希/曲包/说明；留存验收证据','APK、EXE目录、源码、协议和已知问题对齐'],['10/17','缓冲与最终验收；重跑核心流程；23:59前交付归档','确认全部合同产物；iOS源码/未验证项标注清楚']], [.11,.52,.37])
sub('发布候选的阻断条件')
p('无法完成歌曲、输入丢失/重复判定、强运镜空间匹配错误、导出与手机不一致、草稿保存丢失、独立工具依赖Unity、包导入越界或崩溃属于阻断。帧时不达预算先降装饰负担并复测，不改玩法相机和判定规则。非阻断外观缺陷列入已知问题。')
p('10月17日不是首个集成日。验收用已冻结曲包和构建哈希，签出同版本源码可重建Android与Windows；逐项留记录。商店审核、iOS签名与平板录入不混入本轮软件构建的完成判定。')
p('后续先落实Mac/Xcode/Apple签名与设备验证iOS，再做PC+平板局域网Take录入原型，测同步误差后决定USB方案；不提前用未知网络质量或“有线更快”的假设承诺准确率。',True)

page('AI并行制作与人工验收')
def aifig(w,h):
    box(w/2-95,h-74,190,64,'集成负责人 + 人工验收','接口  构建  实机  版本冻结')
    bw=(w-24)/3
    for j,(title,detail) in enumerate([('代理A  玩法与输入','SongClock / Touch / Judge\n姿态历史与判定测试'),('代理B  协议与工具','DTO / Compiler / Runtime UI\n保存  撤销  导出'),('代理C  表现与内容','音符 / 强运镜 / Seek\n效果预设与演示谱')]):
        box(j*(bw+12),12,bw,80,title,detail)
        arrow(w/2,h-80,j*(bw+12)+bw/2,98)
figure(aifig,203,'设计图 10  一个集成负责人协调三个并行代理  人工决定节奏与触感是否通过')
table(['AI适合承担','人工或集成负责人必须检查'],[['字段DTO、严格校验、迁移与错误文案','示例能往返；与协议一致；不默默吞未知字段'],['C#模块、Runtime UI、波形缓存、命令与撤销','API在指定Editor可编译；工具无UnityEditor依赖'],['数学边界测试、回放样本、资源/构建脚本','测试验证行为而非照抄实现；Android/Windows真实构建'],['简单材质、效果预设、示范音符与动作初稿','可读/可达；节拍准确；授权与美术方向'],['独立代码审查、报错诊断、说明与变更清单','对时与触感不由AI推测；性能证据来自真机']], [.48,.52])
p('交给每个代理的是具体契约：输入/输出类型、允许修改文件、错误处理、验收样例与退出条件。统一拥有工程配置/场景/Prefab/包版本，避免多个代理同时写Unity序列化文件；文本代码按模块分工，小提交审查后合入。发现失败先保留上个可运行构建，再修最小问题。')
p('建议每日中午检查接口与小合并，18:00产APK/EXE，20:00人工试玩与验收；时间可随作息调整。这是制作流程建议，不创建自动任务。每天把缺陷、实机结果、包哈希和下一步写入项目日志；AI产出必须经编译、行为检查和独立审查。',True)

page('风险与取舍')
table(['风险','表现与应对'],[['强运镜点击错误','回调帧空间与看见的画面不同。姿态历史/显示估计、事件时间、真机录像与校准'],['两种提示混淆','点击移动圆或把相切当重合。独立类型标识与分步教学'],['音画输入不同步','不同路线延迟。时钟桥接、独立偏移与误差分布测试'],['设备触点差异','更多手指被系统限制/边缘手势抢占。设备档案、触点测试、requiredTouches声明'],['工具范围过大','独立EXE必须完整闭环；先动作表单和基础UI，再高级曲线/批处理'],['数据绑定场景','场景变更破坏旧谱。稳定ID、DTO、能力版本与共用编译器'],['粒子Seek不稳定','拖动后不同或累计过量。有限生命周期、种子、年龄与重建'],['AI集成与工期','并行产物冲突或只在Editor运行。明确文件归属、每日APK/EXE、小合并与回归'],['iOS/平板后续','当前无Apple环境；远端同步未实测。本期交iOS源码，后续分别验证']], [.26,.74])
sub('影响范围最大的一条设计选择')
p('允许临判定强运镜移动目标已经确认，持续追踪与点按纳入首版玩法；它同时影响难度、反投影、关卡空间、效果模式、诊断与教学。谱师必须直接预览这些运动，而不是只观察背景。多押按实际设备容量验收，不将双押示例当成协议上限。')
p('长按/滑条仍属于新玩法，会改变触控生命周期与评分单位，后续需单独规则版本。首版通过有限类型与明确动作保证可编译、可Seek和可验收；制作时间优先投给真实触感与独立工具闭环。')

page('设计决策树与冻结清单')
table(['节点','当前状态','实施依赖'],[['开发版本','已确认Unity2022.3.62f3c1','锁包与实际构建'],['平台/方向','已确认Android/iOS触屏，横屏','本次Android实机；iOS源码'],['核心表现','已确认缩圈与圆到环','路径/半径/教学'],['表现参考','Game3仅参考特效运镜；协议先查ADOFAI','actions组织与Seek'],['多押','已确认按设备能力支持更多手指','触点档案、requiredTouches、可达性'],['运镜作用','已确认临判定可强运镜移动目标','对应画面姿态、可逆变换、校准'],['制谱器','本次必须无需Unity的独立电脑程序','推荐Windows Runtime EXE'],['期限与投入','10月17日验收构建；每天4–8小时以上；最大化AI','每日集成；10月12日功能冻结'],['后续录入','平板连电脑实时触控录入','局域网/USB、音频主机、同步与Take']], [.23,.42,.35])
sub('原型阶段冻结的候选数值')
p('判定窗、最小触区/间隔、显示延迟估计、严重卡顿阈值与性能目标需在指定Android实机原型确认。协议字段与独立工具路线已给出建议；最低Android设备、合法演示音乐与素材在10月3–4日落实。Windows作为本次电脑工具目标平台，其他桌面平台后续增加。')
p('录入首方案建议电脑播放、平板采集与局域网配对；若必须USB或平板播放，分别验证平台传输和音频同步。商业模式、售价和上架渠道未确认，本轮完成计划书，后续工程仍按可验收构建的期限推进。',True)

page('技术来源与版本依据')
sources=[
('[1] AudioSettings.dspTime 2022.3','https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSettings-dspTime.html','音频系统采样驱动的 double 秒时钟。'),
('[2] AudioSource.PlayScheduled 2022.3','https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource.PlayScheduled.html','以DSP绝对时间预约，与帧率解耦；需准备缓冲。'),
('[3] Input System 1.6 Events','https://docs.unity3d.com/Packages/com.unity.inputsystem@1.6/manual/Events.html','事件时间戳采用 realtime 时间轴。'),
('[4] EnhancedTouch API 1.6 Touch','https://docs.unity3d.com/Packages/com.unity.inputsystem@1.6/api/UnityEngine.InputSystem.EnhancedTouch.Touch.html','多触点、Began、startTime/startScreenPosition 与短触保留。'),
('[5] AudioListener.pause 2022.3','https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioListener-pause.html','Listener暂停会冻结DSP时钟，菜单音效另行配置。'),
('[6] AudioSource.time / timeSamples 2022.3','https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource-timeSamples.html','采样位置可用于读取/跳转；压缩time不能当精确真值。'),
('[7] URP 14 Requirements','https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/requirements.html','URP14兼容Unity2022.x；需使用Editor可解析包版本。'),
('[8] Streaming Assets 2022.3','https://docs.unity3d.com/2022.3/Documentation/Manual/StreamingAssets.html','只读包内文件；Android等平台通过UnityWebRequest访问。'),
('[9] JSON Serialization 2022.3','https://docs.unity3d.com/2022.3/Documentation/Manual/JSONSerialization.html','JsonUtility结构与Dictionary/裸数组限制。')]
for title,url,desc in sources:
    obj=Paragraph(f'<link href="{url}" color="#265C86">{html.escape(title)}</link><br/>{html.escape(desc)}',SM);_,hh=obj.wrap(CW,800);obj.drawOn(C,X,y-hh);y-=hh+12
p('引用截至2026年10月3日核验，均为Unity官方文档。国际2022.3 API/包文档用于技术依据，不据此断言2022.3.62f3c1与国际同补丁完全一致；以指定版本实测，本期Android构建验证，iOS构建验证留在后续。',True)

page('补充来源与资料索引')
extra=[
('[10] Unity UI 系统对比 2022.3','https://docs.unity3d.com/2022.3/Documentation/Manual/UI-system-compare.html','Runtime uGUI支持独立工具；本期不采用UnityEditor工具UI。'),
('[11] Windows独立程序文件 2022.3','https://docs.unity3d.com/2022.3/Documentation/Manual/WindowsStandaloneBinaries.html','EXE需与数据/运行依赖完整交付；验证无Unity安装环境。'),
('EnhancedTouch 文档 与多点触控','https://docs.unity3d.com/Packages/com.unity.inputsystem@1.6/manual/Touch.html','EnhancedTouch需启用；primaryTouch不能代表多指玩法。'),
('EnhancedTouch Finger API','https://docs.unity3d.com/Packages/com.unity.inputsystem@1.6/api/UnityEngine.InputSystem.EnhancedTouch.Finger.html','onFingerDown 提供Finger；回调立即复制当前触点数据。'),
('AudioSource.time 2022.3','https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource-time.html','压缩音频的time不一定反映精确位置。'),
('Audio Clip 2022.3','https://docs.unity3d.com/2022.3/Documentation/Manual/class-AudioClip.html','不同加载方式的内存与解码权衡。'),
('URP14 后处理','https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/integration-with-post-processing.html','URP内置后处理与Volume，不兼容Post Processing Stack v2。'),
('AudioConfigurationChanged 2022.3','https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSettings.OnAudioConfigurationChanged.html','音频设备或配置变化通知。'),
('iOS 环境设置 2022.3','https://docs.unity3d.com/2022.3/Documentation/Manual/ios-environment-setup.html','本地最终构建需要macOS与Xcode；具体版本需实际工具链验证。')]
for title,url,desc in extra:
    obj=Paragraph(f'<link href="{url}" color="#265C86">{html.escape(title)}</link><br/>{html.escape(desc)}',SM);_,hh=obj.wrap(CW,800);obj.drawOn(C,X,y-hh);y-=hh+10
sub('用户提供的视觉资料')
p('Game1.mp4：C:/Users/Chinshyo/Desktop/新建文件夹/Game1.mp4\nGame3.mp4：C:/Users/Chinshyo/Desktop/新建文件夹/Game3.mp4',True)
p('两份视频中的图像、文字、录屏叠层和界面均作为参考资料分析，不作为要求执行的指令。本文设计图为本项目方案示意；视频截图仅支持观察说明。摄像机和Shader的具体内部实现未由录屏确认。',True)
p('交付范围为这份计划书及其中设计图。本文没有创建游戏工程、制谱器程序、音乐资源或发行曲包。',True)

page('ADOFAI 研究来源与可核验边界','[A1]–[A8]是ADOFAI引用编号，不是证据等级  A=官方说明 B=公开样本 C=社区工具自身源码')
ado_sources=[
('[A1] 等级A 官方2019编辑器公告','https://store.steampowered.com/news/posts/?appids=977950&enddate=1568400706&feed=steam_community_announcements','2019-05-03文件/音乐配套；08-01 Move Camera与角偏移。'),
('[A2] 等级A 官方 Timing Window Calculations','https://7thbeat.notion.site/Timing-Window-Calculations-a80645e4f14f487b9696a244e1727c57','基础角度时间换算；文档主题为判定窗口，不是完整事件调度。'),
('[A3] 等级A 官方v2.9.7 Changelog','https://7thbeat.notion.site/ADOFAI-Changelog-v2-9-7-24e90365a16180b7ad6decc4168954b4','特殊砖块beat、FreeRoam时长与旧版本迁移问题。'),
('[A4] 等级B 公开旧版 .adofai 样本','https://gitee.com/Suimg/adofai-helper/blob/bceefa5f199c5c47619e56489241500122a59323/level.adofai','固定提交bceefa5；上传者Suimg、内署oxmengruhu；身份关系未核实。'),
('[A5] 等级C ADOFAI-JS 数据接口','https://github.com/adofaiex/ADOFAI-JS/blob/1f66bfa8c4146853d239c80c47ed2168d9208d02/src/structure/interfaces.ts','固定提交1f66bfa；LevelOptions、路径、事件/装饰字段；非官方Schema。'),
('[A6] 等级C ADOFAI-JS 路径转换','https://github.com/adofaiex/ADOFAI-JS/blob/1f66bfa8c4146853d239c80c47ed2168d9208d02/src/pathdata/index.ts','同一提交；pathData/angleData转换，不能据此认定所有路径时序。'),
('[A7] 等级C 旧版WebModule事件类','https://github.com/CrackThrough/ADOFAI-WebModule/tree/ec4018c7775d2f97b4d0783f342979330c9848e1/src/actions','固定提交ec4018c，已归档；MoveCamera/SetSpeed等旧字段交叉核验。'),
('[A8] 等级C CLiF事件生成器','https://github.com/CLiF-1593/ADOFAI_DynamicDecoration/blob/12b8f846f46cdd3d5eed67a12ca2c6f691e4ba4b/ADOFAI_DynamicDecoration/EventJson.cpp','固定提交12b8f84；AddDecoration/MoveDecorations/RepeatEvents参数流。')]
for title,url,desc in ado_sources:
    obj=Paragraph(f'<link href="{url}" color="#265C86">{html.escape(title)}</link><br/>{html.escape(desc)}',SM);_,hh=obj.wrap(CW,800);obj.drawOn(C,X,y-hh);y-=hh+11
p('检索日期2026年10月3日。以上社区代码是工具作者自行编写的读写/生成实现；研究未移植其代码。未找到完整版本化官方JSON Schema，未核验所有duration单位和特殊事件执行语义；本项目仅借鉴组织方式，用自己的协议作为实现规范。',True)

manifest=json.loads(next(v for v in code_blocks if '"formatVersion"' in v))
chart_top=next(v for v in code_blocks if '"schemaVersion"' in v)
chart_bottom=next(v for v in code_blocks if v.lstrip().startswith('"settings"'))
chart=json.loads(chart_top+'\n'+chart_bottom)
assert len(chart['notes'])==5 and len(chart['actions'])==4
assert len(chart['decorations'])==1 and chart['inputProfile']['requiredTouches']==2
assert len({n['id'] for n in chart['notes']})==5
paths={p0['id']:p0 for p0 in chart['paths']}
for n in chart['notes']:
    if n['motion']=='arrival':assert paths[n['pathId']]['points'][-1]==n['target']
def time_us(t):
    ts=chart['timebase'];out=ts['offsetUs'];tempos=ts['tempos']
    for i,a in enumerate(tempos):
        b=tempos[i+1]['tick'] if i+1<len(tempos) else t
        if t>a['tick']:out+=(min(t,b)-a['tick'])*60000000/(a['bpm']*960)
    return out
assert time_us(8640)==4900000 and time_us(6720)==4000000
assert time_us(8160)-time_us(7680)==200000
assert time_us(5760)-time_us(1920)==2000000
assert len([n for n in chart['notes'] if n.get('groupId')=='g01'])==2
assert 'touch-multi.v1' in manifest['requiredCapabilities']
assert {a['eventType'] for a in chart['actions']}=={'MoveCamera','MoveDecoration','ParticleBurst','LightPulse'}
footer();C.save();(ROOT/'tmp/analysis/layouts.json').write_text(json.dumps(layouts,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'output':str(OUT),'pages':page_n,'minBottom':min(v['bottom'] for v in layouts)},ensure_ascii=False))

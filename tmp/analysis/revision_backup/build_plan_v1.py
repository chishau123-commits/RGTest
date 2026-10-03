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
C.setTitle('圆环节奏游戏核心计划书');C.setAuthor('项目策划');C.setSubject('Unity 2022.3.62f3c1 移动端音游 游戏 制谱器 谱面协议')
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
    txt(X,26,'圆环节奏游戏  核心计划书  /  2026年10月3日',8,GRAY)
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
    txt(20,8,'图示为横屏候选布局  目标保护层保持清楚',7.5,HexColor('#ADBFCC'))
def editor(w,h):
    dark(w,h);bar=h-34
    txt(12,h-21,'文件   编辑   谱面   检查   导出',9,white);txt(w-141,h-21,'预览   自动保存已启用',8.5,CYAN)
    C.setFillColor(HexColor('#1E3047'));C.rect(7,110,70,h-150,fill=1,stroke=0);C.rect(w-120,110,113,h-150,fill=1,stroke=0)
    for j,t in enumerate(['选择','缩圈音符','到位音符','路径','相机轨道','特效轨道']):txt(15,bar-26-j*26,t,8.7,white)
    txt(w-111,bar-20,'属性  n002',9,white,True)
    for j,t in enumerate(['判定  2:1:000','预读  2 拍','目标  850,360','半径  52','路径  p01','双押组  无','保护窗  200ms']):txt(w-111,bar-43-j*19,t,8.1,white)
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
    for j,stage in enumerate(['引子  暗蓝雾层','主段  轻平移与粒子','高潮  背景缩放冲击','休止  转场后重定位']):
        xx=(j%2)*(ww+12);yy=h-hh-(j//2)*(hh+15)
        C.setFillColor([HexColor('#17283D'),HexColor('#1C3B55'),HexColor('#3F4D76'),HexColor('#162536')][j]);C.roundRect(xx,yy,ww,hh,6,fill=1,stroke=0)
        for k in range(12):ring(xx+12+(k*37)%int(ww-24),yy+15+(k*23)%int(hh-35),1,GRAY,.5)
        ring(xx+ww*.38,yy+hh*.46,15,CYAN,2,False,'1');ring(xx+ww*.7,yy+hh*.56,15,PURPLE,2,False,'2')
        txt(xx+12,yy+hh-21,stage,9,white,True)
        txt(xx+12,yy+8,['目标保持轮廓','相机变化提前预告','点击层与背景分开','强变化留在空拍'][j],8,white)
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
p('本项目使用 Unity 2022.3.62f3c1，面向 Android 与 iOS。玩家在圆与圆环边缘重合的时刻点击目标，音符以“外环收缩”和“圆移动到接收环”两种方式提示同一个判定时点。视觉演出参考 Game3 的特效与运镜，形成可编辑、可复现的歌曲舞台。')
figure(gameplay,274,'设计图 1  手机游戏候选界面  所有界面图为本项目原创功能示意')
table(['需求状态','内容'],[['已确认','Unity 指定版本；Android/iOS；触屏点击；两种运动表现；丰富特效与运镜；只交付计划书'],['已限定','Game1 用于玩法表现参考；Game3 仅用于特效与运镜参考'],['候选方案','横屏；最多双押；内置制谱器；目标临判定稳定。这些选项需确认后冻结'],['数值性质','判定窗口、尺寸、预算及周期均为首轮测试建议，不是视频测得结果']], [.19,.81])
p('建议先建立“听到节拍、看懂提示、准确点中”的可玩核心，再增加演出。每首歌以同一份谱面驱动游戏与制谱预览，避免工具里可读、手机里难以点击的分歧。',True)

page('阅读路径与设计边界')
table(['阅读目标','页码与内容'],[['理解参考与产品范围','3–5  视频分析  核心体验  首版边界'],['评审玩法与表现','6–12  双运动音符  触控判定  界面  特效运镜'],['评审 Unity 实现路线','13–16  音频时钟  模块结构  生命周期  移动端预算'],['评审制谱能力','17–19  工具布局  工作流程  编辑与诊断'],['对接谱面协议','20–27  曲包  时间与空间  字段  完整 JSON 示例  校验'],['评审后续实时录入','28–29  电脑与平板  采集协议  时间同步'],['安排制作与验收','30–35  测试门槛  里程碑  风险  决策与来源']], [.34,.66])
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
table(['系统','首版必须形成的闭环','后续扩展'],[['游戏','教学；两类点击；校准；暂停；结算；本地选曲','额外玩法类型；线上榜单；社交'],['演出','背景分层；相机轨道；发光；粒子；清晰模式','视频背景；复杂 3D 场景；高级 Shader'],['制谱','音频/波形；BPM；放音符；路径；事件；预览；导出','协作编辑；自动辅助作谱；云分发'],['内容','1 个教学曲包与 1 个演示曲包，每包至少 2 个难度','持续曲库与作者生态']], [.14,.57,.29])
sub('首版可交付标准')
p('玩家在实机完成一首带两种音符与段落演出的歌曲；作者在制谱器从空白创建、预览、校验并导出同格式曲包；游戏导入后与预览的判定时刻和主要画面一致。所有核心操作离线可用。')
p('歌曲、美术和视频素材需具有项目可用授权；视频作为参考资料，不默认授予其音乐、素材或谱面的再发行权。首版内容数量是制作建议，商业模式、售价和团队规模未确认，不纳入收入预测。',True)

page('两种音符的运动与判定')
figure(mechanics,280,'设计图 3  同一目标时刻的两种视觉提示  两种方式均点击目标区域')
sub('外环收缩')
p('内圆圆心 q 与半径 r 固定。预读开始时外环半径 R0 大于 r；进度 u 从 0 到 1，R(u)=r+(R0-r)×(1-u)。u=1 时边缘重合。默认线性，避免临近节拍突然加速；初始 R0 建议为 r 的 2.4 倍。')
sub('圆移动到接收环')
p('接收环圆心 q 与半径 r 固定；移动圆半径也为 r，路径 P(u) 在 u=1 时准确等于 q。默认使用直线或三次 Bézier；弧线可由路径工具构造。到位前不穿过接收中心，避免出现第二个“看起来已经重合”的时刻。轨迹淡线与箭头用于预告，不要求玩家拖动手指跟随。')
p('u=clamp((歌曲视觉时间-出现时刻)/(目标时刻-出现时刻),0,1)。目标之后到最晚判定截止前保留稳定轮廓与迟到提示；到达本身不会自动记分。实际结果来自触点时间与位置，而不是碰撞器是否相交。',True)

page('触控判定与输入仲裁')
table(['项目','首轮测试方案'],[['有效输入','每个手指接触的 Began 样本；记录 touchId、startTime、startScreenPosition；按下一次只判一次'],['空间命中','触点落在目标的可点击圆区；到位音符检测接收环覆盖圆区，不检测移动圆'],['时间误差','Δ=校准后的输入歌曲时间-目标歌曲时间；Δ<0 为早，Δ>0 为晚'],['PERFECT','|Δ|≤35 ms；完整反馈'],['GREAT','35 ms<|Δ|≤70 ms；较弱反馈'],['GOOD','70 ms<|Δ|≤100 ms；提示早晚'],['MISS','没有有效输入，且 judgeNow 严格超过目标+100 ms；judgeNow=歌曲时间+输入校准']], [.22,.78])
p('这组窗口是候选配置，应由移动端实测决定。所有边界按包含关系唯一分区；先消费当前输入批次，再扫描到期音符，防止边界帧先判 MISS。手指持续停留、滑过目标、抬起均不产生第二次点击。触点 ID 可复用，以设备 ID、touchId 与接触起始时间组合去重。')
sub('多目标与双押')
p('候选首版最多双押。只把同时满足空间和时间条件的音符放入候选；同时间戳输入批次采用一对一匹配：先最大化命中数，再最小化总 |Δ|，再最小化归一化距离，最后按稳定 ID 决胜。每触点至多命中一个音符，每音符至多消费一个触点。不为“凑双押”额外延迟单点判断。')
p('空点默认不扣分、不清连击，也不打游戏命中音。正式候选包不得有重叠且判定窗相交的点击区，以减少乱点与仲裁歧义。若以后设置空点惩罚，应作为新规则版本单独测试。UI 区域触点优先交给暂停按钮，不能同时穿透命中音符。',True)

page('评分 难度与教学')
sub('可解释的成绩')
p('候选准确率为 (1.00×Perfect数+0.75×Great数+0.40×Good数)/总音符数；Miss 权重为 0。分数为 round(1,000,000×准确率)，各平台采用明确的半数向上取整。所有音符等权，双押的两个音符各占一个单位。连击只显示连续命中数，不重复加权分数；Good 继续连击，Miss 清零。')
table(['难度','先控制的空间与节奏','候选预读'],[['入门','以单点为主；左右区交替；先缩圈后到位；少量节拍变化','900–1400 ms'],['普通','两种方式混合；跨区移动；少量易分辨双押','700–1100 ms'],['进阶','密度提升；曲线路径；更短预读；双押仍遵守可点击距离','500–850 ms']], [.16,.59,.25])
p('以上区间是测试起点，协议以 leadTicks 保存预读拍长，编译后计算实际毫秒并检查范围。难度不能只靠缩小判定区或增加闪光；同时评估每秒音符数、移动距离/时间、同时预读数量、切换两种提示的频率，以及运镜对寻找目标的负担。')
sub('教学与练习')
p('教学依次介绍静止目标、外环重合、移动圆到位、早晚反馈、双押候选和效果模式；每个步骤有自动示范、无失败练习与一次自主点击。首版练习支持 A/B 循环、逐段重试、节拍器、自动演奏与错误分布；改变播放速度作为后续功能，避免首版把音频变速与判定混在一起。')
p('结算显示准确率、最高连击、各判定数量、Δ直方图与错误段落。FULL COMBO 仅在 Miss=0 时获得；ALL PERFECT 需要全部 Perfect。练习、自动演奏和编辑预览成绩不写入正式最佳记录。',True)

page('手机演奏界面设计')
figure(gameplay,298,'设计图 4  横屏演奏布局  图内数字和统计仅用于说明功能')
sub('层级与触区')
p('目标的实线轮廓与编号位于最高的游戏保护层；时间提示环其次；路径线在两者后方；装饰与背景更低。顶部显示曲名、进度和暂停；底部集中显示连击、短判定与早晚提示，避免手指盖住信息。HUD 不参与相机缩放或旋转。')
sub('屏幕适配')
p('候选谱面画布为 1280×720。在 Safe Area 内等比 Fit，并居中留边；长屏新增部分只扩展背景，不拉伸圆形。统一保存左下原点的谱面坐标。视觉直径建议不少于约 48 个 UI 逻辑单位，默认半径 52 谱面单位，具体映射与拇指可达范围以实机确认。触区最小尺寸与可见圆尺寸共同预览，不能仅扩大隐形触区解决太小的圆。')
p('开启双押时，两个目标需留出两根手指的间隔；提供左右手标识可选项，但不锁定某根手指。刘海、圆角、系统手势边缘和暂停按钮都成为编辑器安全区。竖屏与横竖适配仍是待确认分岔，若选择竖屏，空间校验与界面需要重新评审。',True)

page('选曲 结算与设置流程')
table(['界面','需要的信息','主要操作'],[['选曲','封面；曲名/作者；时长；难度；本地最佳；包版本','搜索；排序；收藏；导入；选择难度'],['难度详情','两种音符比例；双押上限；效果强度；是否校验通过','开始；练习；效果模式；校准'],['暂停','歌曲状态；当前段落；练习标识','继续倒数；重试；设置；退出'],['结算','准确率；分数；连击；计数；早晚分布；错误段落','重试；选曲；练习错误段；查看谱面信息'],['设置','音量；延迟档案；效果；触区提示；性能；触感','试听；校准；恢复默认；切换设备档案']], [.16,.47,.37])
sub('设备校准')
p('将系统音量、歌曲音量与反馈音效分开。触感反馈默认可关闭；其延迟不会回写到判定。校准分为输入/听觉综合偏移与视觉偏移，先让用户试听并连续点按，再显示早晚分布和建议值。使用中位数等稳健统计，排除明显离群点；不得把偶然一次点击当作设备延迟。')
p('偏移按设备与输出方式保存，例如扬声器、有线耳机和蓝牙；切换音频路线时提示重新选择或测试档案。校准改变的是玩家端对时，不修改曲包的音乐对齐 offsetUs。所有效果档位保持相同音符和判定规则。')
sub('导入体验')
p('使用系统文件选择器导入曲包，复制到应用可写目录，显示标题、资源体积、版本与校验结果。失败时给出字段和定位原因；不可只报“文件错误”。内置曲包在首次体验时无需导入。手机制谱器应复用同一文件服务，避免直接暴露不适用的桌面路径。')

page('特效与运镜的段落设计')
figure(storyboard,260,'设计图 5  演出分镜  强冲击主要作用于背景  此图是候选导演方案')
table(['音乐段落','演出候选','操作可读性'],[['引子','冷色雾层；少量漂浮粒子；轻亮度呼吸','单点与较长预读，建立识别'],['主段','缓慢平移；层间视差；小范围轮廓脉冲','目标不在临判定突然移动'],['高潮','背景尺度冲击；局部光爆；碎片与粒子增加','保护层仍保留清楚轮廓与编号'],['休止/转段','背景渐暗；相机重定位；新配色缓慢显现','强转场优先在没有待点击任务的区间']], [.15,.45,.40])
p('演出由作者标记音乐段落和事件起点，不用运行时随机频谱直接决定判定区运动。自动频谱可作为背景辅助，正式镜头与关键闪光仍以谱面数据复现。旋转、视差等可作为项目能力扩展，不能说它们已由样片完整证明。')
p('为制谱器提供“节拍脉冲”“场景展开”“休止转场”“高潮碎片”预设；展开后显示每条参数轨道及其值。预设只是编辑快捷入口，导出结果仍是可检查的轨道和事件。',True)

page('表现分层与相机约束')
table(['层/通道','允许的变化','禁止或需检查'],[['背景场景','连续平移/旋转/缩放；云层；亮度；色彩；粒子','不要改变歌曲判定时钟'],['装饰层','轨迹残影；退场碎片；非交互几何','不能伪装成当前可点目标'],['玩法层','候选轻量一致 2D 变换；圆/环/路径共同变换','不允许透视形变使圆变椭圆；禁止瞬间跳切'],['保护层','编号；边缘；判定反馈；可读性辅助','不受全屏暗场/白闪覆盖到不可辨'],['HUD 与菜单','固定在屏幕安全区','不随歌曲相机移动']], [.19,.43,.38])
sub('移动端目标稳定方案')
p('建议默认背景强运镜、玩法层轻运镜。玩法变换轨道在所有音符 [目标-200 ms, 目标+100 ms] 保护区间内保持常值；密集段保护区间合并后可能覆盖整段，此时只移动背景。相机采样与触点检测都用输入对应的歌曲时间求值，不能用回调时刻的最新画面坐标替代旧输入的位置。')
p('空间反投影使用 inputSong+visualCalibration 对应的显示变换；inputCalibration 只修正时间评分，不能改变触点对应的目标位置。一组圆与环共用玩法变换；到位中心与收缩半径保持同一参照。采用正交 2D 与可逆相似变换，背景可呈现 2.5D 层次。整体强运镜是另一个候选分岔，须另定屏幕速度、预告和可达性验收。')
sub('效果档位与发布检查')
p('完整、清晰、减少运动三档分别缩减 Bloom、粒子、背景冲击、震动和闪光；切换档位只处理装饰表现，玩法目标位置保持一致。编辑器显示暗场遮盖、近白饱和、目标出屏、触区重叠及保护窗内运动警告。闪光强度与持续时间需要可设置，首版避免连续满屏高对比闪烁。')

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
p('本地回放保存曲包/谱面内容哈希、规则版本、平台与偏移、已校准输入歌曲时间、触点谱面坐标、唯一接触 ID、结束状态和结果。正式判定回放可确定重算，粒子跨 GPU 的像素完全一致不作保证。练习循环的每一轮有独立状态，不沿用上一轮 Miss。')

page('移动端实现与性能预算')
table(['项目','候选实现与验收预算'],[['渲染','正交玩法层；URP；背景与后处理单独降档；优先降低全屏透明叠加'],['帧率','首版目标稳定 60 fps；高刷为适配项。目标机实测 P95 帧时≤16.7 ms，P99 与峰值单列'],['分配','演奏稳定阶段托管分配目标为 0 B/帧；预加载与对象池，不每帧生成 JSON 或 LINQ 集合'],['音频','短反馈预加载；歌曲加载策略按内存与解码成本测试；不要所有曲目解压常驻'],['素材','候选曲包上限 200 MiB；解压总量 500 MiB；事件并发上限先以压力谱确定'],['存储','内置资源只读；用户曲包/草稿/缓存存可写目录；提供缓存删除入口'],['发热','连续 20 分钟实机演奏，检查降频、帧时和音画漂移；可自动降装饰档位']], [.22,.78])
sub('平台路径与构建')
p('Android 不直接以桌面 File API 读取包内 StreamingAssets，应通过平台抽象和 UnityWebRequest 访问 [8]。导入使用系统选择器，把授权文件复制到应用目录；不申请无必要的全盘存储权限。iOS 本地最终构建需要 macOS 与 Xcode，Windows 负责 Unity 内容开发不等于能直接产出可安装 iOS 包。')
p('在 Unity 2022.3.62f3c1 创建最小技术样例后记录 Editor、URP、Input System、脚本后端、SDK/NDK/JDK 和 Xcode 实际版本，锁定 packages-lock.json；不要用今天官网的推荐包版本推断该补丁原始配套版本。首版以真实 Android 和 iPhone 为时序验收平台，Editor 模拟触控仅用于快速检查。')
p('设备矩阵至少覆盖中档 Android、较旧支持 iPhone 与一台高刷设备；具体机型及最低系统版本在团队持有设备与 Editor 构建能力核验后冻结。上述预算均为工程目标，不是当前已测得的性能结果。',True)

page('制谱器整体布局')
figure(editor,326,'设计图 7  制谱器桌面候选工作区  移动端采用分页与抽屉保留同一数据模型')
sub('形态候选')
p('推荐游戏内置制谱模式，作者无需安装 Unity。Android/iOS 可提供简化触屏工作区；同源桌面工具更适合精细路径与多轨事件。内置玩家工具、Unity 内部工具、先内部后公开仍待确认；协议与预览核心保持共同边界，不把数据绑到 Unity 场景对象。')
sub('四块工作区')
p('中央舞台支持音符布局、路径控制点、安全区、触区、双押与保护窗预览；下方时间轴显示波形、拍线、音符、相机、特效和段落标记；左侧工具负责选择与放置；右侧属性显示当前对象的音乐时间、位置、半径、路径和曲线。')
p('手机布局中央舞台与时间轴轮换为主视图，属性以底部抽屉编辑；拖动放置与双指缩放属于编辑模式，不复用演奏判定。预览时进入与真实游戏相同的演奏界面，编辑手势全部关闭。',True)

page('制谱流程与时间轴操作')
table(['阶段','作者动作','工具提供的反馈'],[['1 建曲包','导入自有音乐、封面，填标题与作者','时长、采样率、资源信息、保存路径'],['2 对时','设置拍零 offset 与 BPM；标记变速/拍号','节拍器与波形对齐；多个段落交叉检查'],['3 先做节奏','在拍线上放点击音符；选择两种表现','自动播放与简洁画面，不先堆效果'],['4 做空间','移动目标；编辑 Arrival 路径；分组双押','终点锁定；触区重叠与可达性提示'],['5 做演出','标段落；添加相机和效果预设/关键帧','保护区间显示；原始/清晰模式并排检查'],['6 校验试玩','自动演奏；手动实机；定位错误段','错误列表跳转到对象；早晚与空间 Miss'],['7 导出验证','生成曲包，再由游戏重新导入','资源哈希、版本、校验报告与预览一致性']], [.17,.41,.42])
sub('基本操作能力')
p('支持拍线吸附 1/1、1/2、1/4、1/8、1/12、1/16、1/24、1/32；自由 tick 模式补充更细分节奏。PPQ 960 可整除多数常用细分，不能表示的细分必须提示近似误差而非悄悄舍入。可多选、复制、镜像、整段平移与量化；批量移动遵守目标固定和路径终点同步。')
p('时间轴同时显示小节/拍/tick 与绝对秒数，拖动支持音频片段预听和 A/B 循环。修改 BPM 时弹出“保持 tick”或“保持绝对时间后重新量化”的操作选择，并展示受影响对象数量；默认保持 tick，任何重定位可整体撤销。')
p('BPM 辅助检测只产生候选，作者用节拍器确认后写入谱面。不会承诺从音乐自动产出可玩的谱面；节奏密度、双手可达性和两类表现的分配仍需要作者决定。',True)

page('编辑命令 事件工具与诊断')
sub('可靠编辑')
p('所有编辑通过命令事务修改文档模型，记录稳定对象 ID 和修改前后状态；拖动连续动作合并成一次撤销。撤销/重做、剪贴板和模板都不直接修改运行中的场景对象。保存采用临时文件加原子替换，周期自动保存到恢复草稿，移动端切到后台立即保存未提交修改。')
table(['工具','必要能力'],[['路径工具','直线与三次 Bézier；控制点；预览进度；末点与 target 联动；提前到位诊断'],['相机工具','背景/玩法轨道分离；关键帧；曲线；常值保护区间；重叠冲突提示'],['效果工具','参数注册表；预设展开；强度包络；生命周期；随机种子；并发计数'],['时间诊断','音符 hit/appearance 时间；输入Δ；late deadline；BPM分段；偏移来源'],['空间诊断','触区可见；边缘距离；双押重叠；临判定目标运动；宽屏和SafeArea模拟'],['资源诊断','缺失文件；大小/哈希；不支持的类型；包版本与授权备注']], [.22,.78])
sub('从错误直接定位')
p('错误格式为严重级别、代码、对象 ID、JSON 路径、音乐时间和解释。点击错误可定位时间轴与舞台对象；修改后增量复查，导出前跑完整校验。语法/资源/时序矛盾为阻断错误，演出密度或低对比为警告，只有用户明确接受可发布的警告才导出。')
p('“自动演奏通过”只说明目标事件可运行，不能证明真实手指点得准。导出曲包要经过手动实机测试。作品署名、歌曲来源与授权备注可以放 metadata；这些记录用于作者管理，不冒充版权授权证明。',True)

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
table(['字段','类型与要求'],[['id / type','唯一 ID；type 首版仅 tap；其他玩法必须新能力'],['tick / leadTicks','非负整数目标 tick；正整数预读拍长；可编译到负出现时间'],['motion','shrink 或 arrival；只改变表现，不改变点击规则'],['target','长度 2 的有限数数组 [x,y]；为固定接收中心'],['radius','有限正数；默认 52；不能由镜头或皮肤修改判定时序'],['groupId','可选；同拍组只接受同 tick，候选最多 2 个音符'],['shrink','motion=shrink 时必需；startScale>1；进度默认线性'],['pathId','motion=arrival 时必需；引用已有路径；与 shrink 参数互斥'],['sequence','可选非负显示序号；未填由排序生成；双押同组显示共享符号'],['hitSoundAssetId','可选白名单短音效引用；未填用内置反馈']], [.28,.72])
sub('路径对象')
p('路径字段为 id、kind 和 points。kind=line 时 points 恰有两点；kind=bezier3 时恰有四点 P0、P1、P2、P3。末点必须与音符 target 在误差≤0.01 谱面单位内一致；为多音符复用路径时各 target 必须一致，否则需复制路径。编辑器移动目标应连动末点，不能导出断开的终点。')
code('''Line(u) = (1-u) * P0 + u * P1
Bezier3(u) = (1-u)^3 * P0 + 3*(1-u)^2*u*P1
             + 3*(1-u)*u^2*P2 + u^3*P3''',8.8)
p('到位圆默认按参数 u 求路径位置，作者可以看到非匀速路径的时间分布；不默认按弧长匀速。若未来引入弧长参数化，必须新增 motion 曲线能力并改变预览器/编译器一致实现。到位前的任何 u<1 不得使中心已等于 target；相切并非判定，相同圆心和相同半径才是边缘完全重合。')
p('v1 不允许音符目标本身有独立移动轨道；只有一致的玩法层变换可改变其屏幕位置。首版路径经解析端点检查与自适应采样检查，实际相同中心的提前根需可靠诊断，不能只查最后一个控制点。',True)

page('参数轨道与有限事件规范')
table(['结构','必需字段与求值规则'],[['连续轨道 Track','id、target、property、defaultValue、keys[]；每个 target/property 最多一条'],['关键帧 Key','tick、value、curve；tick 严格升序；curve 是通往下一关键帧的曲线'],['插值','linear、smoothstep、step；按编译后歌曲时间插值；首帧前用 defaultValue，末帧后保持末值'],['有限事件 Event','id、type、tick、durationTicks>0、layer、seed 及该类型参数；活跃区间 [T(start),T(end))'],['事件排序','同 tick 按稳定 ID 排序；同种可叠加事件按白名单的组合规则处理']], [.26,.74])
table(['target / property','v1 候选值域与含义'],[['scene / zoom, tx, ty, rotationDeg','zoom>0；tx/ty 为谱面单位；角度为度；作用于背景和装饰场景'],['gameplay / zoom, tx, ty, rotationDeg','只允许可逆一致变换；默认 1/0/0/0；须满足保护窗约束'],['background / opacity, tintR, tintG, tintB','[0,1]；线性 RGB 数值；不遮盖玩法保护层'],['bloom / intensity','≥0；实际渲染上限由引擎和效果档位钳制，候选作者上限 2'],['particleBurst','position、preset、count 正整数、seed 整数；仅 decor/background'],['lightPulse','strength∈[0,1]；按年龄形成衰减包络；只作用于背景亮度']], [.46,.54])
p('位置字段在事件 layer 指定的坐标层中解释。粒子 preset 仅引用内置注册名称，例如 builtin.shards，不接受任意脚本名。v1 lightPulse 的增量为 strength×(1-u)²，多个脉冲求和后钳制到注册范围；临时效果不写回轨道基础值。每帧用基础值+当前事件增量重新构建状态，Seek 不依赖历史累加。',True)

page('完整曲包示例 清单','下列为合法 JSON 结构示例  示例资源名与哈希用于说明  实际曲包由导出器填入文件真实信息')
code('''{
  "formatVersion": "1.0.0",
  "packageId": "demo-ring-001",
  "packageRevision": 1,
  "title": "Ring Demo",
  "artist": "Example Artist",
  "minRuntimeVersion": "1.0.0",
  "requiredCapabilities": [
    "tap.v1", "touch-two.v1", "motion.shrink.v1",
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
p('谱面示例在下两页连续展示，合并后为一个完整顶层 JSON 对象；全例含 5 个音符、2 条路径、1 个双押组、BPM 变化、1 条背景相机轨道和2个演出事件。没有生成实际歌曲或可发行曲包。',True)

page('完整谱面示例 时间 路径与音符','normal.chart.json  上半部分  与下一页接续合并')
code('''{
  "schemaVersion": "1.0.0", "chartId": "normal-001",
  "chartRevision": 1, "ruleVersion": "tap-mobile-1",
  "author": "Example Mapper",
  "difficulty": {"name": "Normal", "rating": 6},
  "inputProfile": "touch-two.v1",
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
code('''  "tracks": [{
    "id": "tr01", "target": "scene", "property": "zoom",
    "defaultValue": 1.0,
    "keys": [
      {"tick": 0, "value": 1.0, "curve": "smoothstep"},
      {"tick": 1920, "value": 1.08, "curve": "smoothstep"},
      {"tick": 3840, "value": 1.0, "curve": "linear"},
      {"tick": 5760, "value": 1.2, "curve": "smoothstep"},
      {"tick": 7680, "value": 1.0, "curve": "step"}
    ]
  }],
  "events": [
    {"id": "ev01", "type": "particleBurst", "tick": 5760,
     "durationTicks": 960, "layer": "decor", "position": [640,360],
     "preset": "builtin.shards", "count": 24, "seed": 17},
    {"id": "ev02", "type": "lightPulse", "tick": 7680,
     "durationTicks": 480, "layer": "background", "strength": 0.25,
     "seed": 0}
  ]
}''',8.8)
sub('示例预期行为')
table(['歌曲时间','预期动作'],[['0.500 s','n001 开始预读；背景保持或沿 zoom 曲线变化'],['1.500 s','n001 缩圈边缘重合；n002 在路径起点开始预读'],['2.500 s','n002 圆心到达接收环；g01 两个音符开始预读'],['3.500 s','n003 与 n004 同时可判定；碎片事件开始'],['4.500 s','切换 150 BPM；背景亮度脉冲开始，持续 200 ms'],['4.900 s','n005 缩圈重合；音符部分结束，歌曲可继续播放']], [.21,.79])
p('tr01 仅作用于 scene，玩法层为恒等变换，因此镜头示例不违反临判定目标稳定约束。两条事件均不影响 hitTime 和分数。作者可加入结束段演出；结算时机由音乐结束策略与最后判定尾窗共同确定，而不是最后一条粒子结束就结算。',True)

page('编译结果 校验与迁移')
table(['错误码','导出或加载的处理'],[['E_VERSION / E_CAPABILITY','主版本或必要能力不支持；阻断并解释所需能力'],['E_RESOURCE / E_PATH','缺文件、大小/哈希不符、越界路径；阻断'],['E_ID / E_FIELD','重复 ID、未知类型、缺必填、NaN/Infinity、互斥字段同时存在；阻断'],['E_TIME / E_TEMPO','负目标时间、BPM≤0、重复变速 tick、空预读、目标晚于音乐；阻断'],['E_GEOMETRY','路径终点不匹配、提前重合、目标不可见、相同窗口触区重叠；阻断'],['E_GROUP / E_TRANSFORM','组内 tick 不同、超过输入上限、玩法相机违反保护窗；阻断'],['W_DENSITY / W_EFFECT','预读拥挤、过度闪光、粒子并发高、背景低对比；警告与定位']], [.39,.61])
sub('发布校验规则')
p('v1 采用固定白名单字段与类型，未知核心字段阻断并指向版本；若未来引入 extensions，须使用命名空间和能力声明，不能现在默默忽略。JSON 解析成功不是谱面合格。JsonUtility 的 Dictionary 与裸数组限制 [9] 需要 DTO 设计或可支持严格字段校验的解析器，所选库须在 Android/iOS IL2CPP 下验证。')
p('ChartCompiler 输出按 hitUs、ID 排序的音符数组，含 appearUs、hitUs、deadlineUs、target/radius、motion/pathIndex/groupIndex；另外输出轨道关键帧歌曲时间、事件区间和空间/时间索引。结果只由作品数据和规则版本生成，不包含玩家偏移，不把运行缓存作为作者格式。')
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
table(['测试面','必要场景','通过标准'],[['时序','所有窗口边界；掉帧；10 分钟长曲；暂停/Seek；偏移正负','相同输入样本得到相同判定；无漏处理/重复处理；记录漂移分布'],['几何','两种运动终点；镜头变换；宽屏/SafeArea；触区边界','重合恰在目标时刻；事件时间投影一致；没有不可点目标'],['多点','同拍2点；同帧短触；ID复用；滑过；UI按下','一触点一音符；不把滑动或长触重复算作点击'],['编辑','撤销/重做；BPM改动；批量复制；恢复；导出重导入','作品语义不变；预览与游戏的时间和路径一致'],['演出','任意Seek；清晰模式；强闪/暗场；并发压力','参数可还原；视觉不改分；目标可读；无持续性能退化'],['移动','中档Android/支持iPhone；音频切换；后台；发热','恢复可解释；正式记录标记一致；达到预算或降低装饰档位'],['后续录入','重复、迟到、断线、代次变更、时钟漂移','不丢失已确认原始样本；不跨代混入；可撤销与复核']], [.14,.40,.46])
sub('玩法与内容的人工验收')
p('至少让新手与有音游经验的玩家分别测试教学、单点段、到位段和双押段。记录定位错误、早晚偏向、手指遮挡、镜头干扰和设备路线；将节奏读不懂、空间点不到与真实输入延迟分开诊断。没有实机和真人反馈前，35/70/100 ms 判定窗及48逻辑单位触区只保持候选。')
p('协议的确定性测试需要预设输入样本与预期结果；表现测试需要帧截图、录屏和Seek复现；性能测试需要Profiler与设备日志。自动演奏或编辑器无报错均不能单独代替手指体验验收。')

page('制作阶段与里程碑')
table(['阶段','交付与退出条件','相对工期建议'],[['A 规则与技术验证','锁定输入、屏幕方向、双押与目标稳定策略；实机音频/触控最小样例','1–2 周'],['B 可玩核心','两种音符；判定；校准；暂停；结算；一首纯玩法谱面','2–3 周'],['C 曲包与最小制谱','时间轴；波形；两种音符；路径；校验；导出导入闭环','3–4 周'],['D 演出编辑','相机/参数轨道；粒子事件；Seek；预设；效果档位','2–3 周'],['E 实机完善','教学曲；演示曲；难度；性能；恢复；打包兼容','2–3 周'],['F 后续触控录入','PC制谱器+平板采集；同步/配对；Take草稿与量化；断线测试','独立评估，原型后排期']], [.16,.65,.19])
p('首版合计约 10–15 个工作周只是能力完整、素材受控且能连续投入的规划区间；不是承诺日期。团队人数、已有代码与美术/音乐来源未知，C/D阶段可能并行，也可能由同一人串行。每阶段以退出条件决定下一阶段，不按日历强行跳过验收。')
sub('优先级顺序')
p('最先验证移动端时钟、TouchBegan、两种提示能否读懂与点中；然后建立曲包和制谱闭环；最后增加演出密度。平板录入属于后续能力，首版保留输入源接口、单调时钟转换和Take数据模型边界，但不因此提前引入联网演奏。')
p('推荐角色分工：玩法/音频程序负责时钟与输入；工具程序负责模型/命令/协议；技术美术负责圆环材质与效果预算；谱师负责对时/空间/演出；测试负责设备矩阵。小团队可兼任，职责和退出条件仍需有人拥有。',True)

page('风险与取舍')
table(['风险','表现与应对'],[['运镜压过玩法','临判定追目标、白闪遮圈。使用保护层、稳定区间与背景分轨'],['两种提示混淆','玩家点击移动圆或把相切当重合。独立类型标识与分步教学'],['音画输入不同步','不同音频路线与平台延迟。事件时间、桥接、独立偏移与分布测试'],['制谱工具范围过大','手机多轨操作成本高。先完成节奏/路径/导出，再做预设与桌面精细编辑'],['数据协议绑定场景','场景改动导致旧谱失效。稳定ID、DTO、版本能力与运行时编译'],['粒子Seek不稳定','拖动后效果不同或累计过量。有限事件、种子、年龄与重建'],['后续远端录入漂移','网络/时钟问题被误当作作者节奏错误。保存原始样本、同步质量、校准与Take复核'],['iOS构建资源不足','只有Windows，无法完成本地Xcode构建。尽早落实macOS/测试设备']], [.26,.74])
sub('影响范围最大的一条设计选择')
p('若选择允许判定前目标随强运镜移动，玩法由“定位后读节拍”变成“持续追踪并点按”。这会同时改变难度模型、输入反投影、关卡空间规则、效果模式、回放与新手教学。建议把这种模式作为明确可选的特殊能力，而不是作者默认随意打开的装饰效果。')
p('同样，加入长按/滑条会改变 touch 生命周期消费与评分单位，不能只增加一个音符颜色。加入三指以上目标会改变握持、可达性和系统手势冲突。先保存这些扩展边界，避免首版协议假装已有完整支持。')

page('设计决策树与冻结清单')
table(['节点','当前状态','后续依赖'],[['开发版本','已确认 Unity2022.3.62f3c1','实际包版本与移动构建验证'],['平台与输入','已确认 Android/iOS 触屏点击','多点时间戳、SafeArea、音频路线'],['核心表现','已确认外环收缩与圆移动到环','路径末点、半径关系、教学'],['表现参考','已确认 Game3 仅参考特效与运镜','轨道分层、Seek、可读性'],['屏幕方向','候选横屏，待确认','画布、手机UI、拇指可达性'],['同时目标','候选最多双押，待确认','group规则、输入上限、谱面校验'],['运镜作用','候选临判定目标稳定，待确认','玩法相机保护区间、效果模式'],['制谱器形态','已需电脑工具接入平板；完整形态待确认','内部Unity工具或独立作者工具的UI/发布'],['后续录入','已确认平板连接电脑触控实时录入','连接方式、音频主机、同步质量与Take']], [.23,.42,.35])
sub('下一轮需要冻结的分岔')
p('推荐横屏、双押上限2、背景强运镜与目标稳定；推荐独立制谱工作区，后续电脑主机与平板采集端沿用同一文档模型。若选择其他分支，先更新受影响规则与图示，再冻结协议。录入连接先建议局域网，电脑播放音乐；若必须USB或平板播放，需先验证各平台传输与音频同步。')
p('商业目标、最低设备、团队规模、内容授权、首次公开发行渠道与工期投入仍待项目立项明确。计划书的字段、流程和验收已经足以支持原型拆分，候选设计不等于已批准开发或发行。全部设计数值需要在实机可玩原型阶段复审。',True)

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
p('引用截至 2026年10月3日核验，均为 Unity 官方文档。国际 Unity 2022.3 API 与包文档用于技术依据，不据此断言 2022.3.62f3c1 与国际同补丁完全一致；最终以用户指定版本和实际 Android/iOS 构建验证。',True)

page('补充来源与资料索引')
extra=[
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

manifest=json.loads(next(v for v in code_blocks if '"formatVersion"' in v))
chart_top=next(v for v in code_blocks if '"schemaVersion"' in v)
chart_bottom=next(v for v in code_blocks if v.lstrip().startswith('"tracks"'))
chart=json.loads(chart_top+'\n'+chart_bottom)
assert len(chart['notes'])==5 and len(chart['events'])==2
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
footer();C.save();(ROOT/'tmp/analysis/layouts.json').write_text(json.dumps(layouts,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'output':str(OUT),'pages':page_n,'minBottom':min(v['bottom'] for v in layouts)},ensure_ascii=False))

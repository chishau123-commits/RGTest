from pathlib import Path
import html, json
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import A4
from reportlab.lib.colors import HexColor, white, black
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, Table, TableStyle
from reportlab.lib.styles import ParagraphStyle

ROOT=Path(r'D:\MyDataInD\Quad')
OUT=ROOT/'output/pdf/圆环音游三天独立需求计划书.pdf'
pdfmetrics.registerFont(TTFont('YH',r'C:\Windows\Fonts\msyh.ttc',subfontIndex=0))
pdfmetrics.registerFont(TTFont('YHB',r'C:\Windows\Fonts\msyhbd.ttc',subfontIndex=0))
W,H=A4; X=43; CW=W-2*X
C=canvas.Canvas(str(OUT),pagesize=A4,pageCompression=1)
C.setTitle('圆环音游三天独立需求计划书')
C.setAuthor('项目计划')
C.setSubject('2026年10月3日至5日 两人自由认领 GitHub PR审核 原型阶段')
BLUE=HexColor('#265C86'); GRAY=HexColor('#586576')
S=ParagraphStyle('body',fontName='YH',fontSize=10.1,leading=16.2,wordWrap='CJK')
SM=ParagraphStyle('small',parent=S,fontSize=8.8,leading=13.5)
TS=ParagraphStyle('cell',parent=S,fontSize=8.7,leading=13.4)
TH=ParagraphStyle('thead',parent=TS,fontName='YHB',textColor=white)
y=0; pn=0; layouts=[]
def txt(x,yy,s,size=10,color=black,bold=False):
    C.setFont('YHB' if bold else 'YH',size); C.setFillColor(color); C.drawString(x,yy,s)
def footer():
    if y<55: raise RuntimeError(f'page {pn} overflow {y}')
    layouts.append({'page':pn,'bottom':round(y,1)})
    txt(X,26,'圆环音游  三天独立需求计划书  /  2026年10月3日',8,GRAY)
    txt(W-X-13,26,f'{pn:02d}',8,GRAY)
def page(title,tag=''):
    global pn,y
    if pn: footer(); C.showPage()
    pn+=1; C.bookmarkPage(f'p{pn}'); C.addOutlineEntry(title,f'p{pn}',0,False)
    txt(X,H-40,'UNITY 2022.3.62f3c1  /  GITHUB PR  /  自由认领',8.5,GRAY)
    txt(X,H-76,title,21,black,True); y=H-101
    if tag: p(tag,True)
def p(s,small=False):
    global y
    v=Paragraph(html.escape(s).replace('\n','<br/>'),SM if small else S)
    _,h=v.wrap(CW,900); v.drawOn(C,X,y-h); y-=h+8
def sub(s):
    global y
    y-=4; txt(X,y,s,12.1,black,True); y-=23
def table(headers,rows,widths):
    global y
    d=[[Paragraph(html.escape(str(v)),TH) for v in headers]]
    d += [[Paragraph(html.escape(str(v)).replace('\n','<br/>'),TS) for v in r] for r in rows]
    t=Table(d,colWidths=[CW*w for w in widths])
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),BLUE),('ROWBACKGROUNDS',(0,1),(-1,-1),[white,HexColor('#F1F5F8')]),('GRID',(0,0),(-1,-1),.45,HexColor('#D9D9D9')),('VALIGN',(0,0),(-1,-1),'MIDDLE'),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6)]))
    _,h=t.wrap(CW,900); t.drawOn(C,X,y-h); y-=h+13
def req(num,title,when,hours,deps,work,accept,proof):
    sub(f'REQ-{num:02d}  {title}')
    p(f'优先级：P0 必做  |  建议：{when}  |  预计：{hours}人时  |  依赖：{deps}',True)
    p('需求：'+work)
    p('验收：'+accept)
    p('PR附件：'+proof,True)
    global y
    y-=4; C.setStrokeColor(HexColor('#D9D9D9')); C.line(X,y,W-X,y); y-=12

page('圆环音游三天独立需求计划书','2026年10月3日至5日  第一阶段原型与GitHub协作流程  最终构建验收仍为10月17日')
p('两名开发者每天各投入4至8小时或更多，按需求自由认领。每条需求具有独立编号、范围、依赖、验收与PR证据；认领时登记实际负责人和修改文件，不预先指定固定分工。PR由非作者的实际指定人员审核，可以互审，也可由后续加入的审核者审批。')
table(['已确认','本阶段落实方式'],[['玩法','Android横屏触屏；缩圈与圆到环两类点击；设备能力内多押；临判定强运镜可移动目标'],['电脑工具','Windows x64独立Runtime程序；无需Unity安装；最简表单建谱、保存、预览、导出'],['协议','沿用核心计划书的tick/paths/settings/decorations/actions方向；先做明确标记的原型子集'],['协作','GitHub Issue认领、短分支、PR审核、检查与集成；审核通过才进入主分支'],['后续','完整制谱器/曲包导入、演出完善继续推进10月17日交付；iOS源码与平板录入边界不变']], [.19,.81])
sub('三天的最终产物')
p('独立EXE从空白建立30至60秒测试谱，保存重开、预览并导出JSON；通过明确的开发传输把文件放到Android，实机演奏两种音符、玩法相机运动与多押，并留下时钟/输入/姿态诊断记录。提交测试APK、EXE完整目录、同版本源码和验收记录。')
sub('投入预算')
p('13条P0需求估计22.5至37.5人时，含实施、自测和常规PR整改；另留4至6人时用于互审、每日集成与闭环排错，合计约26.5至43.5人时。按约36至44人时准备较稳妥，相当于两人各每天6至7.3小时。另有审核者时仍要预留等待/整改时间，AI加速不能省掉实机检查。')
p('两人仅每天各4小时合计24人时，低于完整闭环的估计下限；应优先完成环境、协议、手机时序/触控/运镜和工具启动，未通过项如实留到后续，不把候审或未验收结果标成完成。今天剩余工时不足时，先保住基础环境与接口。',True)

page('独立需求索引与三天顺序')
rows=[
('01','工程与双平台空构建','D1','2至3'),
('02','GitHub协作与PR审核流程','D1','1至2'),
('03','共用原型协议、接口和样例','D1','2至3'),
('04','歌曲时钟与输入时间桥接','D1-D2','2至3'),
('05','多触点采集与接触生命周期','D1-D2','1至2'),
('06','两种音符的几何与时间提示','D2','2至3'),
('07','玩法相机与确定性变换','D2','1.5至2.5'),
('08','移动目标的画面姿态匹配','D2-D3','2至4'),
('09','时间等级与多点输入仲裁','D2-D3','1.5至2.5'),
('10','独立制谱器的最简编辑保存','D1-D2','3至4'),
('11','共用预览与Seek重建','D2-D3','1.5至2.5'),
('12','工具导出与Android开发加载','D3','1至2'),
('13','实机回归、干净构建与归档','D3','2至3')]
table(['REQ','需求标题','建议启动','人时'],rows,[.10,.56,.16,.18])
table(['节点','当天检查'],[['D1 / 10月3日','工程可打开；APK和EXE空构建可运行；协议/样例冻结；音频/触点日志争取完成，最晚D2上午收口'],['D2 / 10月4日','两类视觉、玩法相机、基础编辑保存可演示；REQ-08开始验证，强运镜评分须等该项通过'],['D3 / 10月5日','工具导出→Android加载→实机演奏；回归/审批通过后同一main提交归档；未获审批则候审归档']], [.23,.77])
p('独立需求不等于没有依赖。两人从已解除依赖的需求中自由认领；每人同时只保留一条主要实施需求。契约或同一个场景正在修改时，其他人可以做UI、测试样例与文档，不同时改同一份Unity序列化文件。',True)

page('需求01至02  工程与协作基础')
req(1,'工程与双平台空构建','D1','2至3','无',
    '使用Unity2022.3.62f3c1建立同一工程的Android游戏入口和Windows x64工具入口；核心、游戏、工具使用独立程序集/目录。锁定包与工具链，启用文本序列化和可见.meta，记录构建步骤。[4][5]',
    '指定Editor打开无编译错误；真实Android安装启动APK；Windows EXE连同数据目录可在无需Unity的环境启动。记录Android设备/SDK/NDK/JDK及Windows构建版本，不能只展示Editor中的运行。',
    '两份空构建或下载链接、启动截图、Editor/包版本清单、构建日志。环境缺失应说明尚未完成的步骤。')
req(2,'GitHub协作与PR审核流程','D1','1至2','无，可与REQ-01并行',
    '配置仓库、Issue需求字段、PR提交说明、轻量检查及main审核规则；设置实际审核账号与审查窗口。采用main加短任务分支；仓库可见性与权限由项目确认，不为启用功能擅自公开工程。[1][2]',
    '试验PR可触发检查、请求审核；非PR作者的审核者能拉取提交并作Approve/Request changes，具备被计入有效审批的权限。支持时要求至少一次有效审批、检查通过和讨论解决，禁止未审直推main；新增提交重新核对审批。[1]',
    '仓库设置记录、试验PR链接、检查输出、审核账号与可用时段。未提供仓库URL时先交可应用的配置说明。')
p('私有仓库的受保护分支受GitHub方案限制。先确认当前方案能否强制门禁；若不能，记录“人工执行PR规则”，由具备权限的人按相同清单执行，不能声称GitHub已经自动阻止绕过。[1]',True)

page('需求03至04  数据与歌曲时间')
req(3,'共用原型协议、接口和样例','D1','2至3','可先定契约，编译接入需REQ-01',
    '冻结原型DTO与加载/编译/求值接口。沿用PPQ960、offsetUs、tempo、tap音符、shrink/arrival、直线路径、settings和MoveCamera动作；声明requiredTouches。准备30至60秒自制节拍WAV、合法JSON与缺字段/重复ID/错误终点等反例。',
    '游戏与工具调用同一编译器/求值器；不分别保存tick和冲突的hitSeconds。跨BPM时刻有预期数值。原型标识为prototype-ring-0，写清支持字段；遇到未支持动作/版本明确拒绝，不冒充完整v1兼容。',
    'DTO/接口说明、同一测试JSON、字段支持表、反例和时间换算结果。已有核心计划书第23至30页为完整协议方向；本次子集需注明将来迁移。')
req(4,'歌曲时钟与输入时间桥接','D1启动，D2上午收口','2至3','REQ-01、REQ-03',
    '建立SongClock，预约播放测试节拍WAV；桥接触控realtime时间与DSP歌曲时间。保留输入校准和视觉校准的独立参数、正负号及锚点，运行不靠deltaTime累计歌曲时间。',
    '时间评分=rawInputSong+inputCalibration-hitTime。目标5.000秒、原始输入5.040秒、校准-40ms时误差为0；音频暂停/重启后锚点重建。日志明确输入时间来源，不把处理回调时刻当落指时刻。',
    '时钟日志、校准正负测试、预约播放/重新开始录屏。数值单测核对数学关系，实际听觉延迟以设备记录验证。')

page('需求05至06  输入与两种音符')
req(5,'多触点采集与接触生命周期','D1至D2','1至2','REQ-01、REQ-04',
    '复制每根手指Began的原始时间、起始位置与接触序号；采用能保留触摸变化的输入路径，不只读primaryTouch。支持设备能力内的更多手指，记录系统上限信息和实测结果，处理ID复用和UI接管。',
    '落指一次只入队一次；保持、滑动、抬指不生成第二次点击；同帧短触和多指不被覆盖。实际可支持触点数写入设备报告；requiredTouches超过可用能力时解释并拒绝正式开谱，不静默截掉多出的触点。',
    '多触点录屏与原始样本；2指及设备实测更高触点测试；短触、ID复用、暂停按钮接触记录。三天仅报告已验证设备。')
req(6,'两种音符的几何与时间提示','D2','2至3','REQ-03、REQ-04',
    '实现内圆固定在谱面坐标、外环从大到等半径的缩圈；实现等半径移动圆沿直线到固定接收环的到位提示。两者共享出现/命中时间与相机变换，玩家点击接收目标圆盘。',
    '在hitTime两圆同心/等半径，边缘完全重合；Arrival路径末点等于target且提前不完全重合。点击移动圆途中不算完成；到位不自动计分；判定尾窗仍有清楚的目标轮廓。',
    '目标前/目标时/目标后截图，端点/半径计算样例与两种音符的Android录屏；测试谱同时含两种表现。')

page('需求07至08  强运镜与空间匹配')
req(7,'玩法相机与确定性变换','D2','1.5至2.5','REQ-03、REQ-04、REQ-06',
    '实现玩法层平移、旋转、等比缩放与MoveCamera动作；给定绝对歌曲时间能直接求矩阵。圆/环/路径共同变化，HUD固定安全区；允许临判定目标仍随强运镜移动。',
    '同一时刻Seek得到同一变换；scale>0可逆，目标保持圆形。强运动样例有平移/旋转/缩放；判定窗口内目标可见、尺寸/间隔可触达。清晰模式若实现，只减少装饰，保持玩法轨迹。',
    '时间点与矩阵样例、强运镜录屏、安全区检查；相机和音符共用求值器的调用说明。')
req(8,'移动目标的画面姿态匹配','D2开始，D3验收','2至4','REQ-04、REQ-05、REQ-07',
    '记录约0.5秒玩法呈现姿态历史：帧ID、提交单调时刻、visualSongUs、视口与矩阵。空间匹配根据触点原始时刻和显示延迟估计选择对应快照反投影；时间等级仍用输入校准，不用处理帧的新位置替代旧画面。',
    '按已选快照进行空间检测；快照已含视觉偏移，不重复叠加。延后处理同一输入/快照结果不变。提交时刻不等于实际显示时刻，需用Android录像核验“看见哪里、点哪里”。保持手指等待目标滑入不会自动命中。',
    '输入事件、选中帧/矩阵、显示估计、命中结果日志；延后处理回归与实机录像。显著卡顿使用明确中断状态，不自动扩大窗口或补判。')
p('数学投影可验证一致性，三天内不虚构已知的物理显示延迟或宣称跨设备统一毫秒精度。若此项未通过，强运镜可继续自动预览用于诊断，但不能将相关触控评分标成验收通过。',True)

page('需求09至10  判定与最简制谱')
req(9,'时间等级与多点输入仲裁','D2至D3','1.5至2.5','REQ-03、REQ-04、REQ-05、REQ-06；移动目标验收依赖REQ-08',
    '实现候选35/70/100ms的Perfect/Great/Good及超时Miss；先消费输入再扫超时。触点/音符一对一；候选匹配先尽量命中更多音符，再比较时间/空间误差与稳定ID。记录简单计数和结束状态。',
    '等级边界不重叠；每触点/音符只消费一次，多押独立判定。校准-40ms、原始目标+120ms的输入仍为+80ms Good，不能提前Miss。空点、持指和UI触点不会重复计分；移动目标使用REQ-08结果。',
    '窗口边界与校准反例、同批多点匹配样例、无重复处理日志、Android手动演奏记录。')
req(10,'独立制谱器的最简编辑保存','D1工具壳，D2编辑','3至4','REQ-01、REQ-03',
    '在Windows Runtime EXE提供新建、添加/删除音符、编辑tick/位置/半径/运动类型、Arrival起点及终点、BPM/offset与简单相机动作。使用列表和属性表单即可；保存与重开原型JSON，保存前保留恢复副本。',
    '无需Unity安装即可从空白建测试谱、保存、关闭并重开，语义一致；路径终点与target联动；报错保留当前草稿。基础流程不依赖UnityEditor/EditorWindow，预览/导出按钮连接后续需求。',
    '完整EXE目录、建谱保存重开录屏、样例JSON和恢复副本检查；界面截图即可说明表单，不追求完整作者工作区。')

page('需求11至12  预览与导出贯通')
req(11,'共用预览与Seek重建','D2至D3','1.5至2.5','REQ-04、REQ-06、REQ-07、REQ-10',
    '工具端用共用SongClock/编译器/求值器播放和自动演奏原型谱；提供输入秒数或简单滑条跳转。原型先重建音符与相机，暂停/重开时清空触点与结果。',
    '在目标前/目标时、相机动作中间及BPM边界跳转，结果与连续播放同一时刻一致。切换模式不重复历史命中；回到开头可重新播放。工具与手机同tick对应相同时间、位置和半径。',
    '至少三个Seek点对比截图与数值；连续播放/跳转对照；注明使用的音频定位方式及尚未验证部分。')
req(12,'工具导出与Android开发加载','D3','1至2','REQ-03、REQ-09、REQ-10、REQ-11',
    '导出时检查版本、ID、时间、引用、路径终点和触点需求。提供明确的Android开发加载入口，把EXE导出的同一JSON复制到约定可读位置并加载；可使用ADB、调试包内资源或等效开发方式。测试音频先两端预置同一文件。',
    '更改一个音符tick/target后重新导出，Android实际读取新文件并表现出相同变化；输出文件与加载日志哈希对应；坏数据报对象ID/字段位置。不能由手机另造一份相似谱替代真实导出。',
    '导出JSON及SHA-256、传输/加载步骤、手机加载日志、同曲闭环录屏。若平台路径需要debug入口，写清它只用于原型。')
p('本阶段传输只验证作者数据贯通；正式.ringpack资源/安全校验、系统文件选择器与玩家导入界面仍按后续排期实现。原型子集有明示能力范围，不能直接称为完整曲包协议已完成。',True)

page('需求13与可选需求')
req(13,'实机回归、干净构建与归档','D3','2至3','REQ-01至REQ-12及相关PR审核通过',
    '从同一main提交构建APK与Windows工具目录，跑三天验收清单；准备自制节拍音频、导出测试JSON、配置、构建日志和已知问题。另一台电脑或独立干净目录复核缺失依赖。',
    'Android完成30至60秒两类音符/强运镜/多押段；Windows无需Unity能建谱并导出；回归证据与提交SHA匹配。审核未完成的分支产物明确标候审，不能冒称main交付。保存阶段标签与归档清单。',
    'APK、EXE完整目录、测试谱/音频、SHA与哈希、设备型号/实测触点数、测试结果、未完成/已知问题列表。')
sub('P1 可选需求  在P0与集成时间有余量时认领')
table(['REQ','独立增强需求','依赖与验收'],[['14','直线路径扩展为三次Bézier与控制点编辑','06/10；末点联动、无提前到位，预览/手机一致'],['15','基础网格、拍线吸附与波形缩略','04/10；只改变编辑效率，不改作品时刻'],['16','基础撤销/重做','10；新增/删除/拖动各能还原，保留保存恢复点'],['17','单个背景脉冲或粒子预设','03/07/11；有限生命周期/seed、Seek可还原、目标可读']], [.09,.43,.48])
p('可选需求均可独立开Issue/PR，估计各1至3人时，不计入P0预算。所有增量先确保共用接口兼容；高级多轨UI、完整曲库/结算美术、正式ZIP导入、iOS构建和PC平板录入继续安排到后续。',True)

page('GitHub认领与PR审核规则')
table(['动作','执行规则'],[['认领','Issue对应REQ编号，补实际负责人、修改文件、依赖、目标日期；谁认领谁对该PR自测负责，可协商换人'],['分支','从最新main创建feat/req-06-note-motion等短分支；一个PR围绕一条可验收需求，拆分时注明部分完成'],['依赖','前置接口PR先审；等待时可写独立UI/测试。必须基于候审接口时用Draft并标依赖，前置合并后更新基线重跑'],['文件协调','修改共享DTO/包锁/ProjectSettings/同一场景前登记临时维护人；其他需求用独立组件/Prefab，合并后释放'],['审核','自测后Ready for review，PR给真实审查者；审核结论以GitHub记录为准，AI审查作为辅助证据'],['合并','审批、必要检查和讨论满足后，由当次登记的合并人执行；合入main重跑受影响场景，不固定人员'],['归档','三天阶段用已验收提交建立如prototype-day3标签；构建SHA、资源哈希、测试记录一起保存']], [.17,.83])
sub('每个PR都要让审核者能复现')
p('说明具体问题和最终行为，关联REQ/Issue；给出操作步骤、样例文件、预期结果、测试平台、提交SHA、构建/日志/录屏链接；列出实际覆盖的测试和未完成项。接口或协议变更必须说明影响范围、依赖PR和回归结果，不能只写“已测试”。')
p('main建议至少1次有效审批、轻量检查通过、讨论解决，并将管理员纳入约束；审批后新增变更应重新审核。[1] CODEOWNERS在实际账号/权限落实后用于自动请求审核；它不代替分支规则，也不把自由认领变成固定人员分工。[2]')
p('建议约定每天两个短审查窗口，放在当天开发中点和结束前。每个PR由非作者的指定人员审核；两名开发者可互审，相关时间计入预算。若项目指定另一个人作最终审核，则遵循该约定；其未到场时继续Draft/自测/候审构建，等待有效审批后再合main。',True)

page('检查与验收证据')
table(['检查项','最小场景与结果'],[['时序','35/70/100ms边界、正负偏移、先输入后超时；相同样本得到相同等级'],['多点','2指及设备更高实测容量、同帧短触、ID复用、持指/滑动/UI；无重复/丢失消费'],['几何/运镜','Shrink/Arrival目标时刻重合；平移/旋转/缩放；触点使用对应快照；目标安全可达'],['延后处理','同一原始输入/记录姿态在不同消费频率重算，结果一致；实际显示延迟另用录像核验'],['编辑/Seek','空白建谱、保存重开、原型导出、至少3个Seek点；工具/手机同值'],['闭环','改变tick与坐标后手机加载新JSON，文件哈希与日志对应；不使用另写的相似谱'],['构建','APK实机启动；EXE完整目录无Unity运行；干净目录可打开指定版本工程'],['审核','相关PR审批/检查通过；mainSHA与所有交付证据一致；候审项单列']], [.19,.81])
sub('轻量CI与本地Unity检查')
p('前三天Actions优先运行仓库/样例检查：JSON合法性、必填与引用、样例预期值、冲突标记/意外缓存文件等。先成功运行后再把唯一检查名如repo-validate设为必需，不能让条件跳过代替核心检查通过。具体游戏行为还需Unity测试和真实APK/EXE。[1]')
p('Unity编译/相关数学与判定测试先在指定Editor本地运行，PR附日志。自动Unity构建需要匹配的Editor、模块和许可证环境；条件已齐才接入，前三天不把陌生CI配置排在实机原型之前。批处理入口可按官方参数说明验证。[7]')
p('用Actions artifact保存自动任务输出[3]；本地构建可放在团队可访问的GitHub草稿Release并在PR引用[8]。下载对象必须注明提交SHA和平台。无论上传方式如何，不能拿其他提交的成功日志证明当前PR可运行。',True)
sub('AI使用方式')
p('按REQ契约交给AI生成局部实现、DTO、样例、UI表单、测试与PR说明草稿；限定修改目录，先编译，再跑反例与实机。另一轮AI可独立找接口/平台问题。人工保留试听、触点体验和最终审核；不要同时让多个代理改共享场景和包配置。',True)

page('工程约定与核查来源')
table(['版本控制项','三天约定'],[['提交','Assets及对应.meta、Packages/manifest.json与锁文件、ProjectSettings、源码、协议与测试样例；.meta和资产一起移动/提交[4]'],['忽略','Library/、Temp/、Obj/、Logs/、UserSettings/及构建输出目录；构建包用下载归档，不反复塞进源码提交'],['序列化','确认Force Text与Visible Meta Files；场景/Prefab每次由一条认领需求修改；发生冲突先确认预期对象引用[4][5]'],['合并','文本代码常规合并；UnityYAMLMerge可用于场景/Prefab冲突辅助，合并后仍需打开场景与运行验证[6]']], [.19,.81])
sub('官方来源  核查日期2026年10月3日')
sources=[
('[1] GitHub 受保护分支','https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches','审批、状态检查、管理员约束与可用方案。'),
('[2] GitHub CODEOWNERS','https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners','自动请求审核及所需权限。'),
('[3] GitHub Workflow artifacts','https://docs.github.com/en/actions/concepts/workflows-and-actions/workflow-artifacts','保存工作流输出、测试与构建文件。'),
('[4] Unity2022.3 Asset Metadata','https://docs.unity3d.com/2022.3/Documentation/Manual/AssetMetadata.html','资源GUID与.meta随资产保存。'),
('[5] Unity2022.3 Editor设置','https://docs.unity3d.com/2022.3/Documentation/Manual/class-EditorManager.html','文本序列化与可见.meta设置。'),
('[6] Unity2022.3 Smart Merge','https://docs.unity3d.com/2022.3/Documentation/Manual/SmartMerge.html','UnityYAMLMerge处理场景与Prefab合并。'),
('[7] Unity2022.3 命令行参数','https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html','批处理、工程路径与构建参数。'),
('[8] GitHub 管理Release','https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository','草稿Release、标签和构建文件归档。')]
for title,url,desc in sources:
    obj=Paragraph(f'<link href="{url}" color="#265C86">{html.escape(title)}</link>  {html.escape(desc)}',SM)
    _,hh=obj.wrap(CW,900); obj.drawOn(C,X,y-hh); y-=hh+9
p('需求编号是本计划书的认领编号，不是已创建的GitHub Issue/PR。此轮交付计划书，不创建仓库、推送工程或发送审核请求。完整产品规则沿用此前核心计划书，原型子集/候选阈值须保留明确状态。',True)

footer(); C.save()
(ROOT/'tmp/analysis/three_day_layouts.json').write_text(json.dumps(layouts,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'output':str(OUT),'pages':pn,'minBottom':min(v['bottom'] for v in layouts)},ensure_ascii=False))

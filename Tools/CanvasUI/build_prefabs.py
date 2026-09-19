from pathlib import Path
import re, uuid, json, sys
ROOT=Path(__file__).resolve().parents[2]
def meta(p,folder=False):
 p=Path(p);m=Path(str(p)+'.meta')
 if m.exists(): return re.search(r'guid: (\w+)',m.read_text(encoding='utf-8-sig'))[1]
 g=uuid.uuid4().hex
 kind=('folderAsset: yes\nDefaultImporter:' if folder else 'MonoImporter:' if p.suffix=='.cs' else 'PrefabImporter:')
 m.write_text('fileFormatVersion: 2\nguid: '+g+'\n'+kind+'\n  externalObjects: {}\n'+('  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n' if p.suffix=='.cs' else '')+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8');return g
SCRIPT=meta(ROOT/'Assets/Scripts/Adventure/AdventureCanvasRoot.cs')
GUID={'Image':'fe87c0e1cc204ed48ad3b37840f39efc','RawImage':'1344c3c82d62a2a41a3576d8abb8e3ea','Button':'4e29b1a8efbd4b44bb3f3716e73f07ff','TMP':'f4688fdb7df04437aeb418b961361dc5','Scaler':'0cd44c1031e13a943bb63640046fad76','Raycaster':'dc42784cf147c0c48a680349fa168899','Scroll':'1aa08ab6e0800fa44ae55d278d1423e3','Mask':'3312d7739989d2b4e91e6319e9a96d76','Grid':'8a8695521f0d02e499659fee002a26c2','Vertical':'59f8146938fff824cb5fd77236b75775','Fitter':'3245ec927659c4140ac4f8d17403cc18','Element':'306cc8c2b49d7114eaa3623786fc2126'}
WHITE='830180a310ca4bc48e2d0eed813980e4'
FONT='8f586378b4e144a9851e7b34d9b748ee';TTF='e3265ab4bf004d28a9537516768c1c75'
GOLD=(.82,.70,.47,1);INK=(.025,.032,.032,.97);IVORY=(.92,.89,.81,1);MUTED=(.57,.58,.55,1);RED=(.64,.22,.19,1)
def color(c):return '{r: %s, g: %s, b: %s, a: %s}'%tuple(c)
def ref(i):return '{fileID: '+str(i)+'}'
class Prefab:
 def __init__(self,name,kind,order):
  self.docs=[];self.nodes=[];self.next=100000;self.name=name
  self.root=self.node(name,None,(0,0,1920,1080));self.root['root']=True
  self.component(self.root,223,'Canvas','  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_VertexColorAlwaysGammaSpace: 0\n  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n  m_SortingLayerID: 0\n  m_SortingOrder: '+str(order)+'\n  m_TargetDisplay: 0\n')
  self.mono(self.root,GUID['Scaler'],'  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1920, y: 1080}\n  m_ScreenMatchMode: 1\n  m_MatchWidthOrHeight: 0.5\n  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n  m_DynamicPixelsPerUnit: 1\n')
  self.mono(self.root,GUID['Raycaster'],'  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
  self.mono(self.root,SCRIPT,'  kind: '+str(kind)+'\n  languageFont: {fileID: 12800000, guid: '+TTF+', type: 3}\n  defaultFont: {fileID: 11400000, guid: '+FONT+', type: 2}\n')
 def alloc(self):self.next+=1;return self.next
 def node(self,name,parent,r,active=True):
  n={'id':self.alloc(),'rect':self.alloc(),'name':name,'p':parent,'r':r,'children':[],'components':[],'active':active,'stretch':False};self.nodes.append(n)
  if parent:parent['children'].append(n)
  return n
 def component(self,n,cls,title,body):
  i=self.alloc();n['components'].append(i);self.docs.append((cls,i,title,n,body));return i
 def mono(self,n,guid,body):return self.component(n,114,'MonoBehaviour','  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+guid+', type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n'+body)
 def renderer(self,n):
  if not n.get('renderer'):self.component(n,222,'CanvasRenderer','  m_CullTransparentMesh: 1\n');n['renderer']=True
 def image(self,n,c=INK,ray=False,filled=False,raw=False):
  self.renderer(n)
  b='  m_Material: {fileID: 0}\n  m_Color: '+color(c)+'\n  m_RaycastTarget: '+str(int(ray))+'\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n'
  b+=('  m_Texture: {fileID: 0}\n  m_UVRect: {serializedVersion: 2, x: 0, y: 0, width: 1, height: 1}\n' if raw else '  m_Sprite: {fileID: 21300000, guid: '+WHITE+', type: 3}\n  m_Type: '+('3' if filled else '0')+'\n  m_PreserveAspect: 0\n  m_FillCenter: 1\n  m_FillMethod: 0\n  m_FillAmount: 0.65\n  m_FillClockwise: 1\n  m_FillOrigin: 0\n  m_UseSpriteMesh: 0\n  m_PixelsPerUnitMultiplier: 1\n')
  return self.mono(n,GUID['RawImage' if raw else 'Image'],b)
 def panel(self,parent,name,r,c=INK,active=True,ray=False):
  n=self.node(name,parent,r,active);self.image(n,c,ray);return n
 def text(self,parent,name,r,text='',size=24,c=IVORY):
  n=self.node(name,parent,r);self.renderer(n)
  self.mono(n,GUID['TMP'],'  m_Material: {fileID: 0}\n  m_Color: '+color(c)+'\n  m_RaycastTarget: 0\n  m_Maskable: 1\n  m_text: '+json.dumps(text,ensure_ascii=False)+'\n  m_isRightToLeft: 0\n  m_fontAsset: {fileID: 11400000, guid: '+FONT+', type: 2}\n  m_sharedMaterial: {fileID: 2180264, guid: '+FONT+', type: 2}\n  m_fontColor: '+color(c)+'\n  m_fontSize: '+str(size)+'\n  m_fontSizeBase: '+str(size)+'\n  m_fontWeight: 400\n  m_enableAutoSizing: 1\n  m_fontSizeMin: '+str(max(12,size-5))+'\n  m_fontSizeMax: '+str(size)+'\n  m_fontStyle: 0\n  m_HorizontalAlignment: 1\n  m_VerticalAlignment: 512\n  m_TextWrappingMode: 1\n  m_overflowMode: 1\n  m_isRichText: 1\n  m_parseCtrlCharacters: 1\n  m_isOrthographic: 1\n  m_enableKerning: 1\n  m_extraPadding: 0\n  m_Margin: {x: 0, y: 0, z: 0, w: 0}\n')
  return n
 def button(self,parent,name,r,label):
  n=self.node(name,parent,r);img=self.image(n,INK,True)
  self.mono(n,GUID['Button'],'  m_Navigation:\n    m_Mode: 0\n    m_WrapAround: 0\n    m_SelectOnUp: {fileID: 0}\n    m_SelectOnDown: {fileID: 0}\n    m_SelectOnLeft: {fileID: 0}\n    m_SelectOnRight: {fileID: 0}\n  m_Transition: 1\n  m_Colors:\n    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n    m_HighlightedColor: {r: 1.5, g: 1.4, b: 1.1, a: 1}\n    m_PressedColor: {r: 0.6, g: 0.5, b: 0.4, a: 1}\n    m_SelectedColor: {r: 1.3, g: 1.2, b: 1, a: 1}\n    m_DisabledColor: {r: 0.4, g: 0.4, b: 0.4, a: 0.5}\n    m_ColorMultiplier: 1\n    m_FadeDuration: 0.1\n  m_Interactable: 1\n  m_TargetGraphic: '+ref(img)+'\n  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n')
  self.panel(n,'GoldLine',(8,0,r[2]-16,1),GOLD)
  self.text(n,'Label',(16,3,r[2]-32,r[3]-6),label,24,GOLD)
  return n
 def bar(self,parent,name,r,c=GOLD):
  n=self.panel(parent,name,r,(.12,.13,.12,1));fill=self.node('Fill',n,(0,0,r[2],r[3]));fill['stretch']=True;self.image(fill,c,filled=True);return n
 def portrait(self,parent,name,r):return self.panel(parent,name,r,(1,1,1,1))
 def list(self,parent,name,r,w,h,cols=1):
  scroll=self.node(name,parent,r);viewport=self.panel(scroll,'Viewport',(0,0,r[2],r[3]),(0,0,0,0),ray=True);viewport['stretch']=True
  self.mono(viewport,GUID['Mask'],'  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n')
  content=self.node('Content',viewport,(0,0,r[2],h));content['content']=True
  pad='  m_Padding:\n    m_Left: 0\n    m_Right: 10\n    m_Top: 0\n    m_Bottom: 10\n  m_ChildAlignment: 0\n'
  if cols>1:self.mono(content,GUID['Grid'],pad+'  m_StartCorner: 0\n  m_StartAxis: 0\n  m_CellSize: {x: '+str(w)+', y: '+str(h)+'}\n  m_Spacing: {x: 14, y: 14}\n  m_Constraint: 1\n  m_ConstraintCount: '+str(cols)+'\n')
  else:self.mono(content,GUID['Vertical'],pad+'  m_Spacing: 14\n  m_ChildForceExpandWidth: 1\n  m_ChildForceExpandHeight: 0\n  m_ChildControlWidth: 1\n  m_ChildControlHeight: 1\n  m_ChildScaleWidth: 0\n  m_ChildScaleHeight: 0\n  m_ReverseArrangement: 0\n')
  self.mono(content,GUID['Fitter'],'  m_HorizontalFit: 0\n  m_VerticalFit: 2\n')
  self.mono(scroll,GUID['Scroll'],'  m_Content: '+ref(content['rect'])+'\n  m_Horizontal: 0\n  m_Vertical: 1\n  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n  m_ScrollSensitivity: 35\n  m_Viewport: '+ref(viewport['rect'])+'\n  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n')
  template=self.panel(content,'Template',(0,0,w,h));self.mono(template,GUID['Element'],'  m_IgnoreLayout: 0\n  m_MinWidth: 0\n  m_MinHeight: '+str(h)+'\n  m_PreferredWidth: '+str(w)+'\n  m_PreferredHeight: '+str(h)+'\n  m_FlexibleWidth: -1\n  m_FlexibleHeight: -1\n  m_LayoutPriority: 1\n')
  return template
 def save(self):
  s='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
  for n in self.nodes:
   s+='--- !u!1 &'+str(n['id'])+'\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n'
   for i in [n['rect']]+n['components']:s+='  - component: '+ref(i)+'\n'
   s+='  m_Layer: 5\n  m_Name: '+n['name']+'\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: '+str(int(n['active']))+'\n'
   x,y,w,h=n['r'];stretch=n['stretch'];root=n.get('root');content=n.get('content')
   amin='{x: 0, y: 0}' if stretch else '{x: 0.5, y: 0.5}' if root else '{x: 0, y: 1}'
   amax='{x: 1, y: 1}' if stretch or content else amin
   s+='--- !u!224 &'+str(n['rect'])+'\nRectTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: '+ref(n['id'])+'\n  m_LocalRotation: {x: 0, y: 0, z: '+str(.382683432 if (n.get('rotate') or n.get('spin')) else 0)+', w: '+str(.923879533 if (n.get('rotate') or n.get('spin')) else 1)+'}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n'
   s+='  m_Children:'+ ('\n'+''.join('  - '+ref(c['rect'])+'\n' for c in n['children']) if n['children'] else ' []\n')
   s+='  m_Father: '+ref(n['p']['rect'] if n['p'] else 0)+'\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: '+amin+'\n  m_AnchorMax: '+amax+'\n  m_AnchoredPosition: {x: '+str(0 if stretch or root else x+w/2 if n.get('rotate') or n.get('spin') else x)+', y: '+str(0 if stretch or root else -y-h/2 if n.get('rotate') or n.get('spin') else -y)+'}\n  m_SizeDelta: {x: '+str(0 if stretch or content else w)+', y: '+str(0 if stretch else h)+'}\n  m_Pivot: '+('{x: 0.5, y: 0.5}' if root or n.get('rotate') or n.get('spin') else '{x: 0, y: 1}')+'\n'
  for cls,i,title,n,body in self.docs:
   s+='--- !u!'+str(cls)+' &'+str(i)+'\n'+title+':\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: '+ref(n['id'])+'\n'+body
  folder=ROOT/'Assets/Resources/Adventure/UI';folder.mkdir(parents=True,exist_ok=True);meta(folder,True)
  p=folder/(self.name+'.prefab')
  if p.exists() and '--replace-generated' not in sys.argv:raise SystemExit('Refusing to overwrite edited prefab: '+str(p))
  p.write_text(s,encoding='utf-8');g=meta(p);print(self.name,len(self.nodes),'UI objects');return p,g,self.root['rect']

def summary(p,parent,r):
 n=p.panel(parent,'Summary',r);w=r[2];p.text(n,'Name',(25,18,w-50,58),'Thành viên',35,GOLD);p.text(n,'Progress',(25,86,w-50,46),'Cấp 1 • 0 / 100 EXP',22);p.bar(n,'XP',(25,146,w-50,10));p.text(n,'Details',(25,179,w-50,r[3]-190),'HP 100 / 100\nATK 10    DEF 5\nCrit 10%\nSát thương crit 150%',24)
def picker(p,parent,r):
 n=p.list(parent,'Picker',r,80,94,5);p.portrait(n,'Portrait',(7,7,66,60));p.button(n,'Select',(0,65,80,29),'Chọn');return n
def itemlist(p,parent,name,r,compact=False):
 w=r[2]-10;h=140 if compact else 164;n=p.list(parent,name,r,w,h)
 p.text(n,'Name',(18,12,w-100,45),'Vật phẩm',27,GOLD);p.text(n,'Count',(w-75,12,60,40),'×1',25)
 p.text(n,'Description',(20,65,w-280 if not compact else w-45,70),'Nguyên liệu chế tạo',20,MUTED)
 p.button(n,'Use',(20 if compact else w-250,84 if compact else 88,w-40 if compact else 230,44),'Hồi 40 HP')
 return n

def build_adventure():
 p=Prefab('AdventureCanvas',0,80);r=p.root
 hud=p.node('HUD',r,(0,0,1920,1080),False);hud['stretch']=True
 panel=p.panel(hud,'Leader',(35,32,420,120));p.text(panel,'Name',(20,10,380,42),'Party • Cấp 1',25,GOLD);p.bar(panel,'XP',(25,72,360,9));p.text(panel,'Progress',(25,89,360,24),'0 / 100 EXP',16,MUTED)
 p.text(hud,'Hints',(45,1020,1250,40),'P  Main Menu     E  Tương tác',23)
 p.button(hud,'Interact',(640,925,640,54),'[E] Tương tác')
 menu=p.node('Menu',r,(0,0,1920,1080));menu['stretch']=True
 bg=p.panel(menu,'Background',(0,0,1920,1080),(.006,.012,.014,1),ray=True);bg['stretch']=True
 pattern=p.node('Pattern',menu,(0,0,1920,1080))
 for yy in range(6):
  for xx in range(3):
   diamond=p.node('Diamond_'+str(xx)+'_'+str(yy),pattern,(1490+xx*150,yy*180,115,115));diamond['rotate']=True
   for k,rr in enumerate([(0,0,115,1),(0,114,115,1),(0,0,1,115),(114,0,1,115)]):p.panel(diamond,'Edge'+str(k),rr,(.43,.41,.33,.12))
 preview=p.node('Preview',menu,(350,220,1040,725));p.image(preview,(1,1,1,1),raw=True)
 detailPreview=p.node('DetailPreview',menu,(12,230,670,760),False);p.image(detailPreview,(1,1,1,1),raw=True)
 p.text(menu,'Title',(92,63,1300,100),'HÀNH TRÌNH',64,GOLD);p.panel(menu,'TitleLine',(100,168,565,1),GOLD)
 p.text(menu,'Subtitle',(100,185,1300,42),'Mỗi thành viên mang theo một câu chuyện.',23,MUTED)
 pages={k:p.node(k,menu,(0,0,1920,1080),k=='Party') for k in ['Party','Inventory','Rest','Attributes','Skills','Craft']}
 for page in pages.values():page['stretch']=True
 a=pages['Party'];p.button(a,'Inventory',(100,354,290,54),'Rương đồ');p.text(a,'Section',(100,285,290,54),'PARTY',30,GOLD)
 n=p.list(a,'Cards',(470,788,900,160),400,150,2);p.text(n,'Name',(20,8,355,40),'Thành viên',29,GOLD);p.text(n,'Details',(20,51,355,35),'Cấp 1 • HP 100/100',20);p.bar(n,'HP',(25,102,350,9),RED);p.button(n,'Select',(240,119,135,26),'Chọn')
 p.button(a,'MoveLeft',(485,950,350,45),'← Đổi vị trí');p.button(a,'MoveRight',(865,950,350,45),'Đổi vị trí →')
 p.text(a,'InventoryTitle',(1400,270,410,45),'RƯƠNG ĐỒ',30,GOLD);itemlist(p,a,'Items',(1390,333,440,430),True);p.text(a,'SelectedStats',(1410,790,410,175),'ATK 10 • DEF 5',22)
 p.text(a,'Empty',(1410,350,395,130),'Rương đồ đang trống.',24,MUTED)
 a=pages['Inventory'];picker(p,a,(105,300,480,105));summary(p,a,(105,440,470,370));itemlist(p,a,'Items',(650,285,1090,660));p.text(a,'Empty',(680,350,850,130),'Rương đồ đang trống.',26,MUTED)
 a=pages['Rest']
 for i,(key,label) in enumerate([('Save','Nghỉ ngơi & lưu game'),('Attributes','Nâng thuộc tính'),('Skills','Mở khóa kỹ năng'),('Craft','Chế tạo vật phẩm')]):p.button(a,key,(105,320+i*80,470,58),label)
 p.text(a,'Motto',(110,698,490,140),'Dừng chân. Hồi phục.\nChuẩn bị cho chặng đường tiếp theo.',28,GOLD)
 n=p.list(a,'Members',(1495,310,325,600),315,175);p.portrait(n,'Portrait',(108,0,94,100));p.text(n,'Name',(10,112,295,37),'Thành viên • 100/100',20);p.bar(n,'HP',(22,158,270,9),RED)
 a=pages['Attributes'];picker(p,a,(100,252,480,105));p.text(a,'Points',(620,285,780,60),'ĐIỂM KHẢ DỤNG : 0',30,GOLD);summary(p,a,(1475,300,355,390))
 n=p.list(a,'Stats',(610,390,825,490),805,76);p.text(n,'Name',(20,12,365,45),'Sinh lực',28);p.text(n,'Value',(420,15,215,40),'100 → 110',26,GOLD);p.button(n,'Upgrade',(695,8,92,57),'+')
 p.text(a,'Hint',(625,910,850,48),'Chọn + / - rồi bấm Chấp nhận để áp dụng.',23,MUTED)
 a=pages['Skills'];picker(p,a,(100,250,480,105));p.text(a,'Points',(630,270,760,60),'ĐIỂM KỸ NĂNG : 0',30,GOLD);summary(p,a,(1440,260,395,360))
 n=p.list(a,'Nodes',(590,370,780,550),175,166,4);p.button(n,'Select',(41,15,92,92),'')['rotate']=True;p.portrait(n,'Icon',(58,24,59,57));p.text(n,'Name',(3,120,169,43),'Kỹ năng',18,GOLD);p.text(n,'Rank',(47,78,92,34),'Chưa mở',22)
 detail=p.panel(a,'Detail',(1440,650,395,330));p.text(detail,'Name',(24,15,348,75),'Kỹ năng',31,GOLD);p.text(detail,'Description',(24,100,348,123),'Tăng hiệu lực kỹ năng.',22);p.button(detail,'Upgrade',(20,251,355,52),'Mở khóa • 1 điểm')
 p.text(a,'Empty',(640,410,650,100),'Thành viên chưa có kỹ năng.',30)
 a=pages['Craft'];p.text(a,'Heading',(110,290,480,100),'Từ những gì tìm thấy…',35,GOLD);p.text(a,'Description',(110,410,420,230),'Tận dụng nguyên liệu thu thập trên đường để chuẩn bị vật phẩm cho cả party.',29)
 n=p.list(a,'Recipes',(630,295,1190,675),1165,166);p.text(n,'Name',(28,18,775,50),'Thuốc hồi máu ×1',34,GOLD);p.text(n,'Ingredients',(28,83,735,65),'Thảo dược 0/2 + Quặng 0/1',24);p.button(n,'Craft',(823,86,275,52),'Chế tạo')
 p.button(menu,'Back',(1560,1023,280,44),'[Esc] Trở lại');p.text(menu,'Hints',(100,1030,1250,37),'P Party   B Rương đồ   Q / R Đổi thành viên',20,MUTED)
 toast=p.panel(r,'Toast',(700,15,1060,52),active=False);p.text(toast,'Message',(20,7,1020,38),'Đã lưu game.',20,GOLD)
 # Inventory has a dedicated B menu. Preserve IDs of unrelated authored UI nodes.
 def remove(node):
  for child in list(node['children']):remove(child)
  p.docs=[d for d in p.docs if d[3] is not node];p.nodes.remove(node)
  if node['p']:node['p']['children'].remove(node)
 a=pages['Party']
 for node in list(a['children']):
  if node['name'] in ['Inventory','InventoryTitle','Items','Empty']:remove(node)
 a=pages['Inventory']
 for node in list(a['children']):
  if node['name'] in ['Items','Empty']:remove(node)
 n=p.list(a,'Items',(650,265,710,535),160,120,4)
 p.button(n,'Select',(0,0,160,120),'')
 p.portrait(n,'Icon',(45,12,70,60))
 p.text(n,'Name',(10,75,140,32),'Vật phẩm',18)
 p.text(n,'Count',(107,8,45,29),'x1',20,GOLD)
 p.panel(n,'Selected',(2,117,156,3),GOLD,False)
 p.text(a,'Empty',(1400,720,380,90),'Rương đồ đang trống.',24,MUTED)
 detail=p.panel(a,'Detail',(650,825,710,175))
 p.text(detail,'Name',(20,10,445,35),'Chọn vật phẩm',27,GOLD)
 p.text(detail,'Count',(485,14,210,30),'',20,MUTED)
 p.text(detail,'Description',(20,53,660,56),'Click một ô để xem thông tin vật phẩm.',21)
 p.button(detail,'Use',(20,118,450,43),'Dùng vật phẩm')
 a=pages['Attributes']
 stats=next(n for n in p.nodes if n['name']=='Template' and n['p']['p']['p']['name']=='Stats')
 value=next(n for n in stats['children'] if n['name']=='Value');value['r']=(375,15,200,40)
 p.button(stats,'Reduce',(595,8,82,57),'-')
 p.button(a,'Confirm',(620,910,420,52),'Chấp nhận nâng cấp')
 p.button(a,'Cancel',(1060,910,350,52),'Hủy điểm đang chọn')
 hint=next(n for n in a['children'] if n['name']=='Hint');hint['r']=(620,975,800,34)
 # Party overview: full-height character cards and a separate learned-skill loadout panel.
 a=pages['Party']
 for node in list(a['children']):remove(node)
 blue=(.035,.11,.21,.98);cyan=(.24,.8,1,1)
 overview=p.node('Overview',a,(0,0,1920,1080))
 p.panel(overview,'Backdrop',(70,240,1780,766),blue)
 p.panel(overview,'TopLine',(70,240,1780,2),cyan)
 n=p.list(overview,'Cards',(115,265,1690,625),400,600,4)
 p.panel(n,'CardBlue',(0,0,400,600),(.035,.11,.21,1))
 p.portrait(n,'Portrait',(35,32,330,440))
 raw=p.node('Preview',n,(35,32,330,440));p.image(raw,(1,1,1,1),raw=True)
 p.panel(n,'StatsBackground',(0,415,400,185),(.015,.055,.10,.88))
 p.text(n,'Name',(25,12,350,48),'Thành viên',32,cyan)
 p.text(n,'Details',(20,426,175,118),'Cấp 1\nHP 100/100\nSP 0',23)
 p.bar(n,'HP',(200,460,175,8),cyan)
 p.bar(n,'XP',(200,505,175,8),cyan)
 p.text(n,'Experience',(200,520,175,30),'0 / 100 EXP',18)
 p.button(n,'Select',(22,558,356,37),'Chọn kỹ năng mang theo')
 p.panel(n,'Selected',(0,0,400,3),cyan,False)
 p.button(overview,'MoveLeft',(470,928,390,48),'← Đổi vị trí')
 p.button(overview,'MoveRight',(900,928,390,48),'Đổi vị trí →')
 p.text(overview,'Hint',(130,887,1580,34),'Chọn Party để đổi kỹ năng mang theo hoặc Inventory để xem rương đồ.',21,cyan)
 member=p.node('Member',a,(0,0,1920,1080),False)
 p.panel(member,'Backdrop',(70,240,1780,766),blue)
 p.panel(member,'TopLine',(70,240,1780,2),cyan)
 picker(p,member,(110,275,540,105))
 p.text(member,'SlotsTitle',(110,385,560,45),'KỸ NĂNG MANG THEO',29,cyan)
 n=p.list(member,'Slots',(110,440,550,320),530,64)
 p.button(n,'Select',(0,0,530,64),'Ô 1 • Trống')
 p.button(member,'Remove',(110,778,530,46),'Gỡ kỹ năng khỏi ô đang chọn')
 p.text(member,'LearnedTitle',(720,275,590,45),'KỸ NĂNG ĐÃ HỌC',29,cyan)
 n=p.list(member,'Learned',(710,340,605,550),585,126)
 p.button(n,'Select',(0,0,585,48),'Tên kỹ năng')
 p.text(n,'Description',(18,54,545,68),'Mô tả kỹ năng',19)
 p.text(member,'Empty',(725,360,560,100),'Chưa học kỹ năng nào. Hãy đến điểm nghỉ để học.',24)
 raw=p.node('Preview',member,(1400,265,315,420));p.image(raw,(1,1,1,1),raw=True)
 summary(p,member,(1350,704,445,292))
 sm=next(c for c in member['children'] if c['name']=='Summary')
 for child in sm['children']:
  if child['name']=='Name':child['r']=(20,10,405,44)
  if child['name']=='Progress':child['r']=(20,62,405,36)
  if child['name']=='XP':child['r']=(20,110,405,8)
  if child['name']=='Details':child['r']=(20,135,405,145)
 p.text(member,'Hint',(110,865,1165,96),'Chọn ô rồi click kỹ năng đã học. Thay đổi tự lưu.',25,cyan)
 p.text(pages['Skills'],'Reason',(1440,986,395,40),'',17,MUTED)
 # Main menu: navigation on the left, a non-interactive party preview on the right.
 for node in list(overview['children']):
  if node['name'] in ['MoveLeft','MoveRight']:remove(node)
 cards=next(c for c in overview['children'] if c['name']=='Cards');cards['r']=(525,265,1280,625)
 viewport=cards['children'][0];content=viewport['children'][0];card=content['children'][0]
 card['r']=(0,0,300,600)
 for node in list(card['children']):
  if node['name'] in ['Select','Selected']:remove(node);continue
  x,y,w,h=node['r'];node['r']=(x*.75,y,w*.75,h*.75 if node['name'] in ['Portrait','Preview'] else h)
 for index,(cls,fid,title,node,body) in enumerate(p.docs):
  if node is content:body=body.replace('m_CellSize: {x: 400, y: 600}', 'm_CellSize: {x: 300, y: 600}')
  if node is card:body=body.replace('m_PreferredWidth: 400','m_PreferredWidth: 300')
  if node is viewport:body=body.replace('m_RaycastTarget: 1','m_RaycastTarget: 0')
  if node is cards:body=body.replace('m_Enabled: 1','m_Enabled: 0')
  p.docs[index]=(cls,fid,title,node,body)
 p.button(overview,'Party',(120,320,350,64),'Party')
 p.button(overview,'Inventory',(120,408,350,64),'Inventory')
 hint=next(c for c in overview['children'] if c['name']=='Hint');hint['r']=(120,895,1650,70)
 # Party opens directly to the member loadout page, with a left-hand roster.
 remove(next(c for c in member['children'] if c['name']=='Picker'))
 n=p.list(member,'Picker',(110,285,340,280),320,64)
 p.portrait(n,'Portrait',(5,6,44,52));p.button(n,'Select',(55,0,265,64),'Thành viên')
 placements={'SlotsTitle':(500,255,730,45),'Slots':(500,310,735,300),'Remove':(500,622,715,44),
  'LearnedTitle':(500,690,730,42),'Learned':(500,744,735,190),'Empty':(520,754,670,100),'Hint':(110,945,1190,48)}
 for node in member['children']:
  if node['name'] in placements:node['r']=placements[node['name']]
 p.button(member,'MoveLeft',(110,610,320,44),'← Đổi vị trí')
 p.button(member,'MoveRight',(110,670,320,44),'Đổi vị trí →')
 return p.save()

def build_result():
 p=Prefab('BattleResultCanvas',1,200);r=p.root
 p.panel(r,'Background',(0,0,1920,1080),(.006,.012,.014,.90),ray=True)['stretch']=True
 p.text(r,'Title',(95,55,950,155),'VICTORY',106,GOLD);p.panel(r,'TitleLine',(106,211,644,1),GOLD);p.text(r,'Subtitle',(106,228,1100,50),'CHIẾN THẮNG • HÀNH TRÌNH TIẾP DIỄN',23)
 p.text(r,'Experience',(1390,92,420,110),'0 EXP',68,GOLD);p.text(r,'LootTitle',(110,330,600,60),'Chiến lợi phẩm',38,GOLD)
 n=p.list(r,'Loot',(110,420,600,240),570,55);p.text(n,'Name',(0,0,460,50),'Vật phẩm',27);p.text(n,'Count',(470,0,80,50),'×1',28,GOLD)
 p.text(r,'Description',(110,420,560,230),'Party sẽ trở về điểm lưu gần nhất và được hồi đầy máu.',26)
 n=p.list(r,'Members',(1120,318,710,555),680,155);p.portrait(n,'Portrait',(20,17,95,105));p.text(n,'Name',(140,14,480,43),'Thành viên',32,GOLD);p.text(n,'Progress',(142,63,480,35),'CẤP 1 • 0 / 100 EXP',20);p.bar(n,'XP',(145,112,465,10))
 stats=p.node('Stats',r,(110,685,980,225));p.text(stats,'Heading',(0,0,900,55),'THỐNG KÊ TRẬN ĐẤU',25,GOLD)
 for i,(key,label) in enumerate([('Damage','Sát thương gây ra'),('Highest','Đòn mạnh nhất'),('Received','Sát thương nhận'),('Parries','Parry thành công'),('Kills','Quái bị hạ'),('Time','Thời gian')]):
  col=i//3;row=i%3;p.text(stats,key+'Label',(col*460,66+row*53,310,45),label,25);p.text(stats,key,(320+col*460,66+row*53,125,45),'0',28,GOLD)
 p.button(r,'Continue',(1410,948,410,51),'[Enter] Tiếp tục');p.text(r,'Error',(110,952,1250,50),'',20,RED)
 return p.save()

def build_loading():
 p=Prefab('LoadingCanvas',2,500);r=p.root;p.panel(r,'Background',(0,0,1920,1080),(.006,.012,.014,1),ray=True)['stretch']=True
 p.text(r,'Title',(115,95,1500,80),'HÀNH TRÌNH',60,GOLD);p.text(r,'Subtitle',(120,178,1000,45),'BƯỚC QUA MÀN SƯƠNG',20,MUTED)
 spin=p.node('Spinner',r,(860,380,200,200));spin['spin']=True
 for i,(x,y,w,h) in enumerate([(0,0,200,2),(0,198,200,2),(0,0,2,200),(198,0,2,200)]):p.panel(spin,'Edge'+str(i),(x,y,w,h),GOLD)
 p.text(r,'Message',(330,735,1260,65),'Mỗi bước chân mở ra một câu chuyện.',30);p.bar(r,'Progress',(430,880,1060,10));p.text(r,'Status',(755,920,620,45),'ĐANG TẢI • 0%',20,MUTED)
 p.button(r,'Retry',(765,980,390,45),'Thử về mapgame')
 return p.save()

if __name__=='__main__':
 for f in [build_adventure,build_result,build_loading]:f()

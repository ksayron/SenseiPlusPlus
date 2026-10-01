"""Build editable IDEF0 diagrams and render previews from the saved mxGraph XML.

Run with Python 3 and Pillow; no diagrams.net installation is needed.
"""
from pathlib import Path
import math
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / 'Sensei-IDEF0.drawio'
FONT = Path('C:/Windows/Fonts/arial.ttf')
BOLD = Path('C:/Windows/Fonts/arialbd.ttf')

INPUTS = {
    'I1': 'Исходные понятия, материалы, упражнения и маршруты',
    'I2': 'Цели обучения, ответы, заметки и самооценки',
    'I3': 'Контекст работы, объяснения и сведения о личном вкладе',
    'I4': 'Учётные данные, локальные операции и резервные копии',
    'I5': 'Описание опыта и правки записей и представлений',
}
CONTROLS = {
    'C1': 'Правила подготовки контента и организации обучения',
    'C2': 'Правила оценки, происхождения и интерпретации свидетельств',
    'C3': 'Явные решения пользователя: подтверждение, оспаривание, отзыв, экспорт',
    'C4': 'Политики доступа, приватности, версий, синхронизации и хранения',
}
MECHANISMS = {
    'M1': 'Разработчик / обучающийся',
    'M2': 'Автор материалов и администратор',
    'M3': 'Клиенты, сервер, хранилища данных и средства синхронизации',
    'M4': 'Необязательные средства ИИ и разрешённые интеграции',
}
OUTPUTS = {
    'O1': 'Учебные материалы, маршрут и обратная связь',
    'O2': 'Хронология работы и подтверждение рассмотренной версии',
    'O3': 'История практики, свидетельства и профиль развития',
    'O4': 'Подтверждённый опыт и проверенные материалы представления',
    'O5': 'Доступ, автономные пакеты, согласованные данные и отчёты эксплуатации',
}

FUNCTIONS = {
    'A1': ('Формировать\nучебную базу', 'Понятия, связи, материалы,\nупражнения и версии маршрутов'),
    'A2': ('Планировать и проводить\nобучение', 'Цели, практика, обратная связь,\nповторение и интенсивные режимы'),
    'A3': ('Осмысливать\nинженерную работу', 'Эпизоды, контекст, объяснения,\nобратная связь и подтверждение'),
    'A4': ('Вести свидетельства\nи профиль развития', 'Источники, помощь, история,\nоспаривание и рекомендации'),
    'A5': ('Подготавливать и\nподтверждать опыт', 'Черновики, редакции, личный вклад,\nрепетиция и проверенный экспорт'),
    'A6': ('Обеспечивать доступ\nи сохранность данных', 'Учётные записи, приватность,\nсинхронизация и восстановление'),
}

REQUIREMENTS = [
    ('FR01', 'Вести предметно-независимый каталог понятий, их связей и предпосылок.', 'A1'),
    ('FR02', 'Готовить, проверять, публиковать и деактивировать версии учебного контента.', 'A1'),
    ('FR03', 'Поддерживать шесть типов упражнений, допустимые ответы и правила проверки.', 'A1, A2'),
    ('FR04', 'Вести личные цели и маршруты по общим понятиям без дублирования прогресса.', 'A2'),
    ('FR05', 'Предоставлять материалы и справочные объяснения выбранной темы.', 'A2'),
    ('FR06', 'Проводить сессии на 3/5/10 заданий; сохранять паузу, пропуск и завершение.', 'A2'),
    ('FR07', 'Сохранять ответы и помощь; проверять объективные задания; выдавать обратную связь.', 'A2'),
    ('FR08', 'Поддерживать интенсивную практику, экзаменационный режим и подготовку к интервью.', 'A2'),
    ('FR09', 'Рекомендовать темы и повторения по целям, свидетельствам и актуальности знаний.', 'A2, A4'),
    ('FR10', 'Создавать и архивировать рабочие/учебные эпизоды; фиксировать контекст и вклад.', 'A3'),
    ('FR11', 'Уточнять интерпретацию контекста, обсуждать решения и сохранять открытые вопросы.', 'A3'),
    ('FR12', 'Записывать явное подтверждение точной рассмотренной версии контекста и обсуждения.', 'A3'),
    ('FR13', 'Автоматически вести историю, свидетельства и профиль с источниками и условиями.', 'A4'),
    ('FR14', 'Позволять оспаривать, исправлять и отзывать наблюдения, сохраняя их происхождение.', 'A4'),
    ('FR15', 'Формировать черновики опыта из активности; разрешать прямое создание и правки.', 'A5'),
    ('FR16', 'Вести редакции опыта; подтверждать точные факты, вклад и известный результат.', 'A5'),
    ('FR17', 'Готовить и репетировать рассказ об опыте; экспортировать проверенное представление.', 'A5'),
    ('FR18', 'Управлять учётными записями, входом, ролями и изоляцией данных владельцев.', 'A6'),
    ('FR19', 'Управлять разрешением на раскрытие, выгрузкой и удалением личных данных.', 'A6'),
    ('FR20', 'Загружать пакеты, обучаться без сети и синхронизировать сохранённые результаты.', 'A1, A2, A6'),
    ('FR21', 'Повторять операции без дублей; выявлять конфликты версий; сохранять принятые данные.', 'A2, A3, A4, A5, A6'),
    ('FR22', 'Контролировать состояние системы; вести журналы, резервные копии и восстановление.', 'A6'),
    ('FR23', 'Принимать разрешённый выбранный контекст из интеграций с указанием происхождения.', 'A3, A6'),
    ('FR24', 'Использовать необязательный ИИ для помощи, интерпретаций и черновиков без автоподтверждения.', 'A2, A3, A4, A5'),
]

FLOWS = [
    ('F01', 'A1 → A2 (вход)', 'Опубликованные версии понятий, материалов, упражнений и маршрутов.'),
    ('F02', 'A2 → A4 (вход)', 'Сохранённые попытки, результаты, самооценки и события помощи.'),
    ('F03', 'A4 → A2 (управление)', 'Приоритеты тем и объяснимые рекомендации повторения.'),
    ('F04', 'A3 → A4 (вход)', 'Наблюдения по объяснениям и контексту; источники, помощь и открытые вопросы.'),
    ('F05', 'A3 → A5 (вход)', 'Контекст, роль, решения, вклад и подтверждение рассмотренного обсуждения.'),
    ('F06', 'A4 → A5 (вход)', 'Свидетельства и их ограничения для подготовки достоверного описания опыта.'),
    ('F07', 'A5 → A4 (вход)', 'Подтверждённая редакция опыта и сведения о применении понятий.'),
    ('F08', 'A6 → A2 (вход)', 'Авторизованные автономные учебные операции для проверки по закреплённым версиям.'),
    ('F09', 'A1 → A6 (вход)', 'Опубликованный состав и точные версии контента для автономных пакетов.'),
]


class Page:
    def __init__(self, file, ident, name, w, h):
        self.ident, self.name, self.w, self.h = ident, name, w, h
        diagram = ET.SubElement(file, 'diagram', id=ident, name=name)
        model = ET.SubElement(diagram, 'mxGraphModel', dx=str(w), dy=str(h), grid='1',
                             gridSize='10', guides='1', tooltips='1', connect='1',
                             arrows='1', fold='1', page='1', pageScale='1',
                             pageWidth=str(w), pageHeight=str(h), math='0', shadow='0')
        self.root = ET.SubElement(model, 'root')
        ET.SubElement(self.root, 'mxCell', id='0')
        ET.SubElement(self.root, 'mxCell', id='1', parent='0')
        self.boxes = {}
        self.count = 0

    def cell(self, value, x, y, w, h, size=20, bold=False, border=False, align='center',
             fill='none', ident=None, color='#17212B'):
        self.count += 1
        ident = ident or f'{self.ident}-v{self.count}'
        style = (f'rounded=0;whiteSpace=wrap;html=0;fontFamily=Arial;fontSize={size};'
                 f'fontColor={color};fontStyle={1 if bold else 0};align={align};'
                 f'verticalAlign=middle;spacing=8;fillColor={fill};'
                 f'strokeColor={"#17212B" if border else "none"};strokeWidth=2;')
        c = ET.SubElement(self.root, 'mxCell', id=ident, value=value, style=style,
                          vertex='1', parent='1')
        ET.SubElement(c, 'mxGeometry', x=str(x), y=str(y), width=str(w), height=str(h), **{'as': 'geometry'})
        self.boxes[ident] = (x, y, w, h)
        return ident

    def edge(self, ident, points, source=None, target=None):
        style = ('edgeStyle=none;rounded=0;html=0;strokeColor=#17212B;strokeWidth=2;'
                 'endArrow=block;endFill=1;endSize=10;startArrow=none;'
                 'jumpStyle=arc;jumpSize=10;')
        attributes = dict(id=ident, value='', style=style, edge='1', parent='1')
        for key, ref, point in [('source', source, points[0]), ('target', target, points[-1])]:
            if ref:
                attributes[key] = ref
                x, y, w, h = self.boxes[ref]
                prefix = 'exit' if key == 'source' else 'entry'
                style += f'{prefix}X={(point[0]-x)/w};{prefix}Y={(point[1]-y)/h};{prefix}Dx=0;{prefix}Dy=0;{prefix}Perimeter=0;'
        attributes['style'] = style
        c = ET.SubElement(self.root, 'mxCell', **attributes)
        g = ET.SubElement(c, 'mxGeometry', relative='1', **{'as': 'geometry'})
        for name, point in [('sourcePoint', points[0]), ('targetPoint', points[-1])]:
            ET.SubElement(g, 'mxPoint', x=str(point[0]), y=str(point[1]), **{'as': name})
        if len(points) > 2:
            a = ET.SubElement(g, 'Array', **{'as': 'points'})
            for x, y in points[1:-1]:
                ET.SubElement(a, 'mxPoint', x=str(x), y=str(y))

    def label(self, text, x, y, w, h=60, size=19):
        return self.cell(text, x, y, w, h, size=size, fill='#FFFFFF')

    def frame(self, subtitle, node):
        self.cell('Sensei++ · Функциональная модель IDEF0', 40, 25, self.w-80, 50,
                  size=28, bold=True, align='left')
        self.cell(subtitle, 40, 80, self.w-80, 45, size=21, align='left')
        self.cell(f'Узел: {node}    |    Модель «как будет»    |    Точка зрения: владелец системы и разработчик',
                  40, self.h-60, self.w-80, 40, size=18, align='left', border=True)


def make_context(file):
    p = Page(file, 'context', 'A-0 · Контекст', 2000, 1400)
    p.frame('Цель: связать обучение и инженерную работу с проверяемым развитием и достоверным описанием опыта.', 'A-0')
    b = p.cell('Сопровождать профессиональное\nразвитие разработчика\n\nОбучение · осмысление работы ·\nсвидетельства · подтверждённый опыт\n\nA0', 700, 480, 580, 340,
               size=27, bold=True, border=True, fill='#FFFFFF', ident='context-A0')
    for i, (code, name) in enumerate(INPUTS.items()):
        y = 515 + i*64
        p.edge('context-'+code, [(70, y), (700, y)], target=b)
        p.label(f'{code}  {name}', 75, y-53, 575, 50, 19)
    for i, (code, name) in enumerate(OUTPUTS.items()):
        y = 515+i*64
        p.edge('context-'+code, [(1280, y), (1930, y)], source=b)
        p.label(f'{code}  {name}', 1330, y-53, 590, 50, 19)
    for i, (code, name) in enumerate(CONTROLS.items()):
        x = 755+i*155
        y = 200 + (i%2)*130
        p.edge('context-'+code, [(x, y+65), (x, 480)], target=b)
        p.label(f'{code}  {name}', x-145, y-35, 290, 95, 18)
    for i, (code, name) in enumerate(MECHANISMS.items()):
        x = 765+i*150
        p.edge('context-'+code, [(x, 1100), (x, 820)], target=b)
        p.label(f'{code}  {name}', 80+i*485, 1130, 470, 90, 20)
        # Labels are arranged in four columns; connect their centres to their vertical arrows.
        label_x = 315+i*485
        c = p.root.find(f"mxCell[@id='context-{code}']/mxGeometry")
        c.find("mxPoint[@as='sourcePoint']").set('x', str(label_x))
        c.find("mxPoint[@as='sourcePoint']").set('y', '1120')
        a = ET.SubElement(c, 'Array', **{'as': 'points'})
        for px, py in [(label_x, 1050+i*12), (x, 1050+i*12)]:
            ET.SubElement(a, 'mxPoint', x=str(px), y=str(py))
    p.cell('ICOM: слева — вход; сверху — управление; справа — выход; снизу — механизм.\nПодтверждение обсуждения, подтверждение фактов опыта и разрешение экспорта являются отдельными решениями.',
           140, 1240, 1720, 65, size=19)
    return p


def make_decomposition(file):
    p = Page(file, 'decomposition', 'A0 · Декомпозиция', 2400, 1570)
    p.frame('Шесть функций полного целевого продукта. Повторённый код граничной стрелки обозначает тот же поток A-0.', 'A0')
    positions = {'A1': (300,350), 'A2': (1000,350), 'A3': (1700,350),
                 'A4': (1000,1000), 'A5': (1700,1000), 'A6': (300,1000)}
    refs = {}
    for code, (x,y) in positions.items():
        name, detail = FUNCTIONS[code]
        refs[code] = p.cell(f'{name}\n\n{code}', x,y,280,170,
                            size=24,border=True,fill='#FFFFFF',ident='decomposition-'+code)
    # Free-ended boundary arrows repeat parent ICOM identifiers; no hidden tunnels.
    for code, name, target, y in [
        ('I1','Исходный контент','A1',415), ('I2','Цели, ответы, самооценки','A2',455),
        ('I3','Контекст и объяснения','A3',480), ('I4','Учётные данные,\nлокальные операции, копии','A6',1020),
        ('I5','Описание опыта и правки','A5',1080)]:
        x = positions[target][0]
        p.edge('decomposition-'+code, [(x-230,y),(x,y)], target=refs[target])
        if code=='I2': p.label(f'{code}  {name}',x-240,400,230,50,17)
        else: p.label(f'{code}  {name}',x-240,y-70,230,68,17)
    for code, name, source, y in [
        ('O1','Материалы, маршрут,\nобратная связь','A2',400),
        ('O2','Хронология и подтверждение\nрассмотренной версии','A3',395),
        ('O3','История, свидетельства,\nпрофиль развития','A4',1040),
        ('O4','Подтверждённый опыт,\nпроверенное представление','A5',1040),
        ('O5','Доступ, пакеты, согласованные\nданные, отчёты эксплуатации','A6',1095)]:
        x=positions[source][0]+280
        p.edge('decomposition-'+code,[(x,y),(x+315,y)],source=refs[source])
        if code=='O1': p.label('O1  Учебные результаты',1465,335,170,60,17)
        elif code=='O3': p.label('O3  История и профиль',1465,950,170,60,17)
        elif code=='O5': p.label('O5  Доступ, пакеты,\nданные и отчёты',690,1100,210,70,17)
        else: p.label(f'{code}  {name}',x+30,y-66,290,62,17)
    controls = {'A1':[('C1','Контент и обучение'),('C4','Версии и доступ')],
                'A2':[('C1','Учебные правила'),('C2','Правила оценки'),('C4','Доступ и версии')],
                'A3':[('C2','Происхождение'),('C3','Явное подтверждение'),('C4','Приватность')],
                'A4':[('C2','Правила свидетельств'),('C3','Оспаривание, отзыв'),('C4','Доступ и хранение')],
                'A5':[('C2','Достоверность'),('C3','Подтверждение, экспорт'),('C4','Доступ и версии')],
                'A6':[('C3','Разрешения пользователя'),('C4','Доступ, хранение, sync')]}
    for target, items in controls.items():
        x,y=positions[target]
        for i,(code,label) in enumerate(items):
            px=x+40+i*(200/(len(items)-1))
            # Stagger labels to avoid an unreadable single row of adjacent names.
            ly=y-145-(i%2)*50
            p.edge(f'decomposition-{code}-{target}',[(px,ly+70),(px,y)],target=refs[target])
            p.label(f'{code}\n{label}',px-60,ly,120,65,14)
    mechanism_sets={'A1':['M2','M3'],'A2':['M1','M3','M4'],'A3':['M1','M3','M4'],
                    'A4':['M1','M3'],'A5':['M1','M3','M4'],'A6':['M1','M2','M3','M4']}
    short_m={'M1':'Разработчик','M2':'Автор / администратор','M3':'Клиенты и хранилища','M4':'ИИ / интеграции (опц.)'}
    for target,codes in mechanism_sets.items():
        x,y=positions[target]
        for i,code in enumerate(codes):
            px=x+40+i*(200/(len(codes)-1))
            sy=y+300+(i%2)*70
            p.edge(f'decomposition-{code}-{target}',[(px,sy),(px,y+170)],target=refs[target])
            p.label(f'{code}\n{short_m[code]}',px-58,sy+5,116,75,14)
    edges = [
        ('F01','A1','A2',[(580,395),(750,395),(750,385),(1000,385)],(650,325,300,55),'F01  Версии учебного контента'),
        ('F02','A2','A4',[(1280,465),(1360,465),(1360,880),(950,880),(950,1080),(1000,1080)],(1205,735,140,65),'F02  Результаты и помощь'),
        ('F03','A4','A2',[(1280,1000),(1450,1000),(1450,150),(950,150),(950,300),(1090,300),(1090,350)],(1190,160,250,55),'F03  Приоритеты и рекомендации'),
        ('F04','A3','A4',[(1980,455),(2110,455),(2110,802),(900,802),(900,1110),(1000,1110)],(1480,700,200,75),'F04  Наблюдения по объяснениям'),
        ('F05','A3','A5',[(1980,505),(2200,505),(2200,940),(1645,940),(1645,1030),(1700,1030)],(2010,860,180,70),'F05  Контекст, вклад и решения'),
        ('F06','A4','A5',[(1280,1140),(1550,1140),(1550,1115),(1700,1115)],(1320,1185,280,60),'F06  Свидетельства и ограничения'),
        ('F07','A5','A4',[(1980,1130),(2290,1130),(2290,1460),(830,1460),(830,1140),(1000,1140)],(1305,1398,440,55),'F07  Подтверждённые сведения о применении понятий'),
        ('F08','A6','A2',[(580,1030),(665,1030),(665,570),(880,570),(880,490),(1000,490)],(690,585,320,60),'F08  Автономные учебные операции'),
        ('F09','A1','A6',[(580,475),(620,475),(620,935),(295,935),(295,1045),(300,1045)],(70,830,200,70),'F09  Состав и версии пакета'),
    ]
    for code,source,target,points,label,text in edges:
        p.edge('decomposition-'+code,points,refs[source],refs[target])
        p.label(text,*label,size=16 if code=='F02' else 18)
    return p


def table(p, rows, widths, x=60, y=175, row_h=49, size=19):
    for ri,row in enumerate(rows):
        px=x
        for ci,(value,w) in enumerate(zip(row,widths)):
            p.cell(value,px,y+ri*row_h,w,row_h,size=size,bold=ri==0,border=True,
                   fill='#EAF0F5' if ri==0 else '#FFFFFF',align='left' if ci==1 else 'center')
            px+=w


def make_requirements(file):
    p=Page(file,'requirements','Требования · Покрытие',2000,1570)
    p.frame('24 функциональных требования целевого продукта и соответствующие им блоки A0.', 'Справочная страница')
    table(p,[('Код','Функциональное требование','Блоки')]+REQUIREMENTS,[120,1460,300],row_h=49,size=19)
    p.cell('Источники: целевое ТЗ (docs/12), исходное видение (product-brief), учебный контракт (docs/09).\nFR08, FR17, FR23, FR24 раскрывают общее видение; сроки и степень реализации здесь не моделируются.',
           60,1430,1880,66,size=20,align='left')
    return p


def make_rules(file):
    p=Page(file,'rules','Потоки · Проверка',2400,1570)
    p.frame('Семантика межфункциональных стрелок и проверка модели по условиям задания.', 'Справочная страница')
    table(p,[('Поток','Откуда → куда / роль','Содержание')]+[(c,d,t) for c,d,t in FLOWS],
          [120,430,1730],y=160,row_h=60,size=20)
    checks=[
        'Выполнено: два уровня — A-0 содержит один блок A0; A0 раскрывает его шестью блоками A1–A6.',
        'Выполнено: все 24 требования имеют хотя бы один блок; ни один блок не остаётся без требований.',
        'Выполнено: модель окружения содержит 6 блоков — соблюдены минимум 4 и ограничение сложности 2–6.',
        'Выполнено: граница сбалансирована — I1–I5, C1–C4, M1–M4, O1–O5 представлены на обоих уровнях.',
        'Выполнено: внутренние потоки выходят справа; входы слева; F03 входит сверху как управление.',
        'Выполнено: механизмы входят снизу; повтор кода внешней стрелки означает тот же граничный поток.',
    ]
    for i,text in enumerate(checks):
        p.cell(text,60,800+i*52,2280,49,size=23,align='left')
    p.cell('Правила движения данных',60,1130,2280,45,size=25,bold=True,align='left')
    p.cell('1. A2 фиксирует ответ, версию задания и помощь до передачи результата в A4; чтение материала не доказывает знание.\n'
           '2. A4 различает проверенный результат, самооценку, применение в работе и интерпретацию ИИ; спорные выводы исключаются из рекомендаций.\n'
           '3. Подтверждение обсуждения в A3 не подтверждает факты опыта в A5; экспорт требует отдельной проверки представления.\n'
           '4. A6 передаёт F08 в A2 для предметной проверки: клиент не может назначить оценку или изменить профиль напрямую.\n'
           '5. Обработка повторов не создаёт дубли; конфликт версий не перезаписывает данные; ИИ не совершает подтверждение за пользователя.',
           60,1180,2280,225,size=22,align='left')
    return p


def style_map(value):
    return dict(x.split('=',1) for x in value.split(';') if '=' in x)


def render(diagram, destination):
    """Render saved native cells/geometry, including crossings as line bridges."""
    model=diagram.find('mxGraphModel')
    w,h=int(model.get('pageWidth')),int(model.get('pageHeight'))
    image=Image.new('RGB',(w,h),'white')
    draw=ImageDraw.Draw(image)
    root=model.find('root')
    segments=[]
    for c in root.findall("mxCell[@edge='1']"):
        g=c.find('mxGeometry')
        nodes=[g.find("mxPoint[@as='sourcePoint']")]+list(g.findall('Array/mxPoint'))+[g.find("mxPoint[@as='targetPoint']")]
        pts=[(float(n.get('x')),float(n.get('y'))) for n in nodes]
        for a,b in zip(pts,pts[1:]):
            crossings=[]
            if a[0]==b[0]:
                for u,v in segments:
                    if u[1]==v[1] and min(u[0],v[0])+1<a[0]<max(u[0],v[0])-1 and min(a[1],b[1])+1<u[1]<max(a[1],b[1])-1:
                        crossings.append((a[0],u[1]))
            elif a[1]==b[1]:
                for u,v in segments:
                    if u[0]==v[0] and min(u[1],v[1])+1<a[1]<max(u[1],v[1])-1 and min(a[0],b[0])+1<u[0]<max(a[0],b[0])-1:
                        crossings.append((u[0],a[1]))
            draw.line([a,b],fill='#17212B',width=2)
            for x,y in crossings:
                if a[0]==b[0]:
                    draw.rectangle((x-3,y-10,x+3,y+10),fill='white')
                    draw.arc((x-9,y-10,x+9,y+10),-90,90,fill='#17212B',width=2)
                else:
                    draw.rectangle((x-10,y-3,x+10,y+3),fill='white')
                    draw.arc((x-10,y-9,x+10,y+9),180,360,fill='#17212B',width=2)
            segments.append((a,b))
        a,b=pts[-2],pts[-1]
        dx,dy=b[0]-a[0],b[1]-a[1]
        length=math.hypot(dx,dy)
        if length:
            dx,dy=dx/length,dy/length
            draw.polygon([b,(b[0]-12*dx+5*dy,b[1]-12*dy-5*dx),
                          (b[0]-12*dx-5*dy,b[1]-12*dy+5*dx)],fill='#17212B')
    overflow=[]
    for c in root.findall("mxCell[@vertex='1']"):
        s=style_map(c.get('style'))
        g=c.find('mxGeometry')
        x,y,bw,bh=[float(g.get(k)) for k in ['x','y','width','height']]
        if s['fillColor']!='none':
            draw.rectangle((x,y,x+bw,y+bh),fill=s['fillColor'])
        if s['strokeColor']!='none':
            draw.rectangle((x,y,x+bw,y+bh),outline=s['strokeColor'],width=2)
        size=int(s['fontSize'])
        font=ImageFont.truetype(str(BOLD if s['fontStyle']=='1' else FONT),size)
        lines=[]
        for paragraph in c.get('value').split('\n'):
            words=paragraph.split()
            line=''
            for word in words:
                candidate=(line+' '+word).strip()
                if line and draw.textlength(candidate,font=font)>bw-16:
                    lines.append(line); line=word
                else: line=candidate
            lines.append(line)
        lh=size*1.17
        if len(lines)*lh>bh-4:
            overflow.append((c.get('id'),c.get('value'),len(lines)*lh,bh))
        ty=y+(bh-len(lines)*lh)/2
        for line in lines:
            tw=draw.textlength(line,font=font)
            tx=x+8 if s['align']=='left' else x+(bw-tw)/2
            draw.text((tx,ty),line,font=font,fill=s['fontColor'])
            ty+=lh
    image.save(destination)
    return overflow


def validate(file):
    context=file.find("diagram[@id='context']/mxGraphModel/root")
    decomposition=file.find("diagram[@id='decomposition']/mxGraphModel/root")
    expected=set(INPUTS)|set(CONTROLS)|set(MECHANISMS)|set(OUTPUTS)
    actual=set()
    for c in decomposition.findall("mxCell[@edge='1']"):
        code=c.get('id').split('-')[1]
        if code in expected: actual.add(code)
    assert actual==expected, f'Unbalanced ICOM: {expected-actual}'
    assert sum(c.get('id')=='context-A0' for c in context)==1
    assert len(FUNCTIONS)==6
    covered=set()
    for _,_,refs in REQUIREMENTS:
        blocks=set(refs.split(', ')); assert blocks<=set(FUNCTIONS); covered|=blocks
    assert covered==set(FUNCTIONS)
    for diagram in file.findall('diagram'):
        root=diagram.find('mxGraphModel/root')
        ids=[c.get('id') for c in root]
        assert len(ids)==len(set(ids))
        for c in root.findall("mxCell[@edge='1']"):
            g=c.find('mxGeometry')
            pts=[g.find("mxPoint[@as='sourcePoint']"),g.find("mxPoint[@as='targetPoint']")]
            for key,point in zip(['source','target'],pts):
                ref=c.get(key)
                if not ref: continue
                target=root.find(f"mxCell[@id='{ref}']/mxGeometry")
                x,y,w,h=[float(target.get(k)) for k in ['x','y','width','height']]
                px,py=float(point.get('x')),float(point.get('y'))
                if key=='source': assert px==x+w and y<=py<=y+h, c.get('id')
                else:
                    code=c.get('id').split('-')[1]
                    if code.startswith('I') or code in {'F01','F02','F04','F05','F06','F07','F08','F09'}:
                        assert px==x and y<=py<=y+h,c.get('id')
                    elif code.startswith('C') or code=='F03': assert py==y and x<=px<=x+w,c.get('id')
                    elif code.startswith('M'): assert py==y+h and x<=px<=x+w,c.get('id')
    print(f'PASS: 24 requirements, 6 blocks, 9 internal flows, {len(expected)} balanced boundary flows; sides and references valid.')


if __name__=='__main__':
    file=ET.Element('mxfile',host='app.diagrams.net',modified='2026-10-01T00:00:00.000Z',
                    agent='Sensei++ IDEF0 generator',version='24.7.17',type='device',compressed='false')
    for build in [make_context,make_decomposition,make_requirements,make_rules]: build(file)
    ET.indent(file,space='  ')
    ET.ElementTree(file).write(OUTPUT,encoding='utf-8',xml_declaration=True)
    saved=ET.parse(OUTPUT).getroot()
    validate(saved)
    preview=ROOT/'previews'; preview.mkdir(exist_ok=True)
    for diagram in saved.findall('diagram'):
        problems=render(diagram,preview/(diagram.get('id')+'.png'))
        assert not problems, f'TEXT OVERFLOW {diagram.get("id")}: {problems}'
        print('RENDER OK',diagram.get('id'))

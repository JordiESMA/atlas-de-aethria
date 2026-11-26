from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT, TA_RIGHT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether

from pathlib import Path
OUT = str(Path(__file__).with_name("Guia_de_juego_Atlas_de_Aethria.docx"))
PDF_OUT = str(Path(__file__).with_name("Guia_de_juego_Atlas_de_Aethria.pdf"))
doc = Document()
sec = doc.sections[0]
sec.top_margin = Inches(.68); sec.bottom_margin = Inches(.65)
sec.left_margin = Inches(.78); sec.right_margin = Inches(.78)

normal = doc.styles["Normal"]
normal.font.name = "Arial"; normal.font.size = Pt(10.5); normal.font.color.rgb = RGBColor(39, 49, 55)
normal.paragraph_format.space_after = Pt(5)
for name, size in (("Title", 28), ("Heading 1", 17), ("Heading 2", 12)):
    style = doc.styles[name]
    style.font.name = "Arial"; style.font.size = Pt(size); style.font.bold = True
    style.font.color.rgb = RGBColor(0, 0, 0)
    style.paragraph_format.space_before = Pt(9 if name != "Title" else 0)
    style.paragraph_format.space_after = Pt(5)

def shade(cell, color):
    pr = cell._tc.get_or_add_tcPr()
    fill = OxmlElement("w:shd"); fill.set(qn("w:fill"), color); pr.append(fill)

def borders(table):
    tblPr = table._tbl.tblPr
    b = OxmlElement("w:tblBorders")
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        e = OxmlElement("w:" + edge); e.set(qn("w:val"), "single"); e.set(qn("w:sz"), "5"); e.set(qn("w:color"), "D9DEE2"); b.append(e)
    tblPr.append(b)

def set_cell(cell, text, bold=False, color=(39,49,55)):
    cell.text = text
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    for p in cell.paragraphs:
        p.paragraph_format.space_after = Pt(2); p.paragraph_format.space_before = Pt(2)
        for run in p.runs:
            run.font.name = "Arial"; run.font.size = Pt(9.5); run.font.bold = bold; run.font.color.rgb = RGBColor(*color)

p = doc.add_paragraph(style="Title"); p.add_run("Atlas de Aethria")
p = doc.add_paragraph(); p.paragraph_format.space_after = Pt(12)
r = p.add_run("Guía de instalación y juego"); r.font.name="Arial"; r.font.size=Pt(14); r.font.bold=True; r.font.color.rgb=RGBColor(178,126,40)
doc.add_paragraph("Esta guía explica cómo instalar Atlas de Aethria y jugar en local o en una partida directa entre dos ordenadores. La meta es expandir tu reino, desarrollar sus edificios y conquistar la capital rival.")

doc.add_heading("Instalar y empezar", level=1)
for text in [
    "Descarga Atlas-de-Aethria-P2P-Entrega.zip y extrae todo su contenido en una carpeta.",
    "Abre la carpeta AtlasDeAethria y ejecuta Atlas de Aethria.exe. No muevas el .exe fuera de esa carpeta: necesita sus archivos de datos y bibliotecas.",
    "Lee la pantalla Antes de zarpar. Puedes escoger una partida local o una sala online directa P2P.",
    "En local, dos personas juegan por turnos en el mismo ordenador. Para jugar online, sigue la sección Partida directa entre jugadores."
]:
    doc.add_paragraph(text, style="List Number")

doc.add_heading("Cómo funciona un turno", level=1)
steps = [
    ("1 · Tira una vez", "En local, pulsa Tirar dados. En una sala, la tirada es automática. Tu capital gana 1 tropa en tu tirada, incluso con un 7. El borde dorado marca la capital reforzada y los números que coinciden."),
    ("2 · Recoge producción", "Los terrenos de ambos jugadores producen con su número desde nivel 1. Por acierto, el huerto da 2 de comida, la fábrica 2 materiales, la mina 3 de oro y el cuartel 2 tropas en su casilla. Cada mejora añade 1 unidad. Con un 7 solo llega el refuerzo de la capital."),
    ("3 · Juega tus acciones", "Selecciona una casilla para ver sus opciones. Puedes comprar, mejorar, entrenar, mover o atacar tantas veces como te permitan tus recursos y tus tropas."),
    ("4 · Finaliza cuando quieras", "Pulsa TERMINAR TURNO para pasar a la otra persona. El turno no termina automáticamente al hacer una acción; tienes que finalizarlo. Cada persona tira una vez al empezar su turno.")
]
for title, body in steps:
    p = doc.add_paragraph(); p.paragraph_format.space_after = Pt(2)
    rr=p.add_run(title); rr.bold=True; rr.font.size=Pt(11); rr.font.color.rgb=RGBColor(0,0,0)
    q=doc.add_paragraph(body); q.paragraph_format.left_indent=Inches(.18); q.paragraph_format.space_after=Pt(6)

doc.add_page_break()
doc.add_heading("Acciones y costes", level=1)
rules_overview = "Los 20 territorios forman una cuadrícula de 5 × 4. Son vecinos si comparten un lado. Hay 6 cuarteles, al menos uno junto a cada capital. Las capitales tienen nivel fijo. Un territorio conquistado conserva su nivel o empieza en N1 si era neutral."
doc.add_paragraph(rules_overview)
t = doc.add_table(rows=1, cols=3); t.alignment=WD_TABLE_ALIGNMENT.CENTER; t.autofit=False
t.columns[0].width=Inches(1.35); t.columns[1].width=Inches(2.05); t.columns[2].width=Inches(3.5)
for c, text in zip(t.rows[0].cells, ("Acción", "Coste", "Qué hace")):
    shade(c,"18343D"); set_cell(c,text,True,(255,255,255))
rows = [
    ("Comprar", "5 oro", "Reclama una casilla neutral vecina, coloca 1 tropa y establece nivel 1."),
    ("Mejorar", "N2: 5 oro + 2 materiales. N3: 7 oro + 3 materiales.", "Añade 1 unidad por acierto a partir de la siguiente tirada. N3 es el nivel máximo."),
    ("Entrenar", "2 comida + 2 oro", "Añade 1 tropa en tu capital o en un cuartel propio de nivel 2 o 3."),
    ("Mover", "Sin coste de recursos", "Mueve 1 tropa entre dos casillas propias vecinas; deja al menos 1 tropa en el origen."),
    ("Atacar", "Sin coste de recursos", "Ataca una casilla neutral o enemiga vecina con al menos 2 tropas. Cada lado suma sus tropas y un dado de seis caras; gana la suma más alta y, en empate, defiende la casilla. Si atacas y pierdes, pierdes 1 tropa.")
]
for i,row in enumerate(rows):
    cells=t.add_row().cells
    for c, text in zip(cells,row):
        set_cell(c,text)
        if i%2==1: shade(c,"F1F5F6")
borders(t)
for row in t.rows:
    for cell, width in zip(row.cells, (1.35, 2.05, 3.5)):
        cell.width = Inches(width)

yields=[("Territorio","Nivel 1","Nivel 2","Nivel 3"),("Cuartel","2 tropas","3 tropas","4 tropas"),("Huerto","2 de comida","3 de comida","4 de comida"),("Fábrica","2 materiales","3 materiales","4 materiales"),("Mina","3 de oro","4 de oro","5 de oro")]
doc.add_heading("Producción por acierto", level=1)
income_table=doc.add_table(rows=0, cols=4)
income_table.alignment=WD_TABLE_ALIGNMENT.CENTER
for i, row in enumerate(yields):
    for cell, value in zip(income_table.add_row().cells, row):
        set_cell(cell,value,bold=(i==0),color=(255,255,255) if i==0 else (39,49,55))
        if i==0: shade(cell,"18343D")
borders(income_table)

doc.add_heading("Objetivo y final", level=1)
doc.add_paragraph("Conquistar la capital rival termina la partida inmediatamente. Si se completan 30 rondas sin que caiga una capital, gana quien tenga más puntos. Cada territorio vale 1 punto, cada tropa 1 punto, cada edificio 2 puntos y cada capital 5 puntos. Si el resultado es igual, hay empate.")

doc.add_page_break()
doc.add_heading("Partida directa entre jugadores (P2P)", level=1)
doc.add_paragraph("No hay cuentas ni servidor central. El ordenador de una persona aloja la partida y la otra se conecta directamente. El juego incluye Node.js en el paquete para Windows.")
invite_steps = [
    "El anfitrión entra en Jugar online · P2P, selecciona Crear sala y pulsa Iniciar sala. Mantiene el juego abierto durante la partida.",
    "En la sala, revisa la dirección y pulsa Copiar enlace para invitar. Envía el enlace completo al rival.",
    "El invitado abre Atlas una primera vez para registrar las invitaciones. Después puede abrir el enlace o pegarlo en Unirse con invitación. Si escribe solo la dirección, debe añadir el código de seis caracteres.",
    "En la misma Wi-Fi se usa la IPv4 local. Si Windows solicita acceso, permite Node.js solo en redes privadas de confianza. Desde otra red se necesita una dirección pública alcanzable y el reenvío de TCP 3080 en el router del anfitrión.",
    "El código identifica la sala. CGNAT puede impedir conexiones entrantes. El protocolo utiliza HTTP sin cifrar y está pensado para redes de confianza."
]
for i,text in enumerate(invite_steps,1): doc.add_paragraph(f"{i}. {text}")

doc.add_heading("Consejos para la primera partida", level=1)
for text in [
    "Mira el número de cada territorio. Si es tuyo, produce con esa suma en la tirada de cualquiera.",
    "El borde dorado señala los territorios activados; el color azul o rojo identifica a quien tiró.",
    "Compra terrenos variados: cada especialidad produce un recurso distinto y te permite ampliar tus opciones.",
    "Mantén tropas en el camino hacia la capital rival y deja defensas en tu propio reino."
]: doc.add_paragraph(text, style="List Bullet")

doc.add_heading("Controles", level=1)
doc.add_paragraph("Ratón: pulsa los botones y las casillas. Para jugar localmente, J1 y J2 comparten el mismo teclado y ratón y se alternan al terminar cada turno.")

footer=sec.footer.paragraphs[0]; footer.alignment=WD_ALIGN_PARAGRAPH.RIGHT
rr=footer.add_run("ATLAS DE AETHRIA  ·  GUÍA RÁPIDA"); rr.font.name="Arial"; rr.font.size=Pt(8); rr.font.color.rgb=RGBColor(100,110,113)
doc.save(OUT)
print(OUT)

# Guía de dos páginas para imprimir y compartir.
navy = colors.HexColor("#18343D")
gold = colors.HexColor("#B27E28")
ink = colors.HexColor("#273137")
line = colors.HexColor("#D9CDB7")
cream = colors.HexColor("#F6F0E2")
styles = getSampleStyleSheet()
def style(name, size, leading, font="Helvetica", color=ink, **opts):
    return ParagraphStyle(name, fontName=font, fontSize=size, leading=leading, textColor=color, **opts)
title_style = style("Page title", 23, 27, "Helvetica-Bold", navy, spaceAfter=7)
head_style = style("Section", 14, 17, "Helvetica-Bold", navy, spaceBefore=11, spaceAfter=5)
body_style = style("Body", 10, 13.5, spaceAfter=5)
cell_style = style("Cell", 9.2, 12)
table_head = style("Table head", 9.2, 12, "Helvetica-Bold", colors.white)
num_style = style("Step number", 15, 18, "Helvetica-Bold", gold)
def para(text, s=body_style):
    return Paragraph(text, s)
def matrix(data, widths, settings):
    table = Table(data, colWidths=[w*mm for w in widths], hAlign="LEFT")
    table.setStyle(TableStyle([("VALIGN",(0,0),(-1,-1),"MIDDLE"),
        ("TOPPADDING",(0,0),(-1,-1),5),("BOTTOMPADDING",(0,0),(-1,-1),5)] + settings))
    return table
def frame(canvas, document):
    w,h=A4
    canvas.saveState()
    canvas.setFillColor(navy); canvas.rect(0,h-52,w,52,stroke=0,fill=1)
    canvas.setFillColor(gold); canvas.circle(44,h-26,8,stroke=0,fill=1)
    canvas.setFillColor(colors.white); canvas.setFont("Helvetica-Bold",11); canvas.drawString(63,h-24,"ATLAS DE AETHRIA")
    canvas.setFont("Helvetica",8); canvas.drawString(63,h-37,"GUÍA DE INSTALACIÓN Y JUEGO")
    canvas.setStrokeColor(line); canvas.line(44,40,w-44,40)
    canvas.setFillColor(navy); canvas.drawString(44,26,"Estrategia por turnos para dos jugadores")
    canvas.drawRightString(w-44,26,f"{document.page} / 2"); canvas.restoreState()

story = [para("Instala, invita y juega",title_style),
    para("Dos reinos comparten un mapa de 20 territorios. Gana quien conquiste la capital rival o sume más puntos al cabo de 30 rondas."),
    para("Instalar y empezar",head_style),
    para("1. Extrae todo el ZIP. Dentro de AtlasDeAethria, abre <b>Atlas de Aethria.exe</b> junto a sus archivos."),
    para("2. Lee <b>Antes de zarpar</b> y elige partida local para compartir ordenador, o <b>Jugar online · P2P</b> para conectar dos equipos."),
    para("Cómo funciona un turno",head_style)]
story.append(matrix([[para(f"{i:02}",num_style),para(f"<b>{name.split(' · ',1)[1]}</b><br/>{body}",cell_style)] for i,(name,body) in enumerate(steps,1)],
    [15,163],[("BACKGROUND",(0,0),(-1,-1),cream),("GRID",(0,0),(-1,-1),.5,line)]))
story.append(para("Acciones y costes",head_style))
data=[[para(s,table_head) for s in ("Acción","Coste","Efecto")]]+[[para(s,cell_style) for s in row] for row in rows]
story.append(matrix(data,[25,43,110],[("BACKGROUND",(0,0),(-1,0),navy),
    ("ROWBACKGROUNDS",(0,1),(-1,-1),[colors.white,cream]),("GRID",(0,0),(-1,-1),.5,line)]))
story += [Spacer(1,7),para("Al final de 30 rondas, cada territorio y cada tropa valen 1 punto. Un edificio suma 2 y una capital añade 5. A igual puntuación hay empate."),
    PageBreak(),para("Invita a otra persona",title_style),
    para("El ordenador del anfitrión mantiene la partida. Ambos jugadores deben usar la misma versión. Atlas incluye el servicio de sala."),
    para("Crear sala y compartir el enlace",head_style)]
for i, instruction in enumerate(invite_steps[:3],1):
    story.append(para(f"<b>{i}.</b> {instruction}"))
story += [para("Conexión en la misma Wi-Fi",head_style),
    para("Comparte la IPv4 local de la sala. En una red de confianza, el anfitrión puede usar el perfil privado de Windows y autorizar Node.js solo para redes privadas."),
    para("Conexión desde otra red",head_style),
    para("La conexión directa requiere una dirección pública alcanzable y el reenvío de TCP 3080 hacia el anfitrión. Cambia la dirección de la sala antes de copiar el enlace. CGNAT puede impedir el acceso. El protocolo usa HTTP sin cifrar. Juega en redes de confianza."),
    para("Lee el tablero",head_style),para(rules_overview),
    para("La cartela indica la tirada de producción, el escudo indica el nivel y la ficha T cuenta las tropas. Selecciona un territorio para ver su rendimiento y el coste de la mejora."),
    para("Producción por acierto",head_style)]
story.append(matrix([[para(s,table_head if i==0 else cell_style) for s in row] for i,row in enumerate(yields)],
    [43,45,45,45],[("BACKGROUND",(0,0),(-1,0),navy),("ROWBACKGROUNDS",(0,1),(-1,-1),[colors.white,cream]),("GRID",(0,0),(-1,-1),.5,line)]))
story += [Spacer(1,7),para("Usa el ratón para elegir casillas y acciones. En partida local, los dos jugadores se alternan al pulsar <b>Terminar turno</b>.")]
SimpleDocTemplate(PDF_OUT, pagesize=A4, rightMargin=16*mm, leftMargin=16*mm, topMargin=25*mm,
    bottomMargin=19*mm,title="Atlas de Aethria · Guía de instalación y juego").build(story,onFirstPage=frame,onLaterPages=frame)
print(PDF_OUT)

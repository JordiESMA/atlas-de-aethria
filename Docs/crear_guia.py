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
    ("1 · Tira una vez", "Pulsa TIRAR. Se muestran las dos caras, la suma y las casillas cuyo número coincide. El borde dorado y el resplandor azul o rojo permanecen hasta que finalices el turno. No puedes volver a tirar durante ese turno."),
    ("2 · Recoge producción", "Los edificios construidos de ambos reinos cuyo número coincide producen: cada mina da +3 oro, cada huerto +2 comida, cada fábrica +2 materiales y cada cuartel +1 tropa. Un 7 no activa edificios."),
    ("3 · Juega tus acciones", "Selecciona una casilla para ver sus opciones. Puedes comprar, construir, entrenar, mover o atacar tantas veces como te permitan tus recursos. No hay límite de acciones."),
    ("4 · Finaliza cuando quieras", "Pulsa TERMINAR TURNO para pasar a la otra persona. El turno no termina automáticamente al hacer una acción; tienes que finalizarlo. Cada persona tira una vez al empezar su turno.")
]
for title, body in steps:
    p = doc.add_paragraph(); p.paragraph_format.space_after = Pt(2)
    rr=p.add_run(title); rr.bold=True; rr.font.size=Pt(11); rr.font.color.rgb=RGBColor(0,0,0)
    q=doc.add_paragraph(body); q.paragraph_format.left_indent=Inches(.18); q.paragraph_format.space_after=Pt(6)

doc.add_page_break()
doc.add_heading("Acciones y costes", level=1)
rules_overview = "El tablero tiene 20 territorios en una cuadrícula de 5 × 4; solo son vecinos los que comparten un lado. Comprar un territorio neutral lo deja en nivel 1; construir en él lo sube a nivel 2. Las capitales empiezan en nivel 2."
doc.add_paragraph(rules_overview)
t = doc.add_table(rows=1, cols=3); t.alignment=WD_TABLE_ALIGNMENT.CENTER; t.autofit=False
t.columns[0].width=Inches(1.35); t.columns[1].width=Inches(2.05); t.columns[2].width=Inches(3.5)
for c, text in zip(t.rows[0].cells, ("Acción", "Coste", "Qué hace")):
    shade(c,"18343D"); set_cell(c,text,True,(255,255,255))
rows = [
    ("Comprar", "5 oro", "Reclama una casilla neutral vecina, coloca 1 tropa y establece nivel 1."),
    ("Construir", "5 oro + 2 materiales", "Desarrolla una casilla propia de nivel 1 a nivel 2. El terreno determina qué produce."),
    ("Entrenar", "2 comida + 2 oro", "Añade 1 tropa en tu capital o en un cuartel propio construido."),
    ("Mover", "Sin coste de recursos", "Mueve 1 tropa entre dos casillas propias vecinas; deja al menos 1 tropa en el origen."),
    ("Atacar", "Sin coste de recursos", "Ataca una casilla neutral o enemiga vecina con al menos 2 tropas. Cada lado suma sus tropas y un dado de seis caras; gana la suma más alta y, en empate, defiende la casilla. Si atacas y pierdes, pierdes 1 tropa.")
]
for i,row in enumerate(rows):
    cells=t.add_row().cells
    for c, text in zip(cells,row):
        set_cell(c,text)
        if i%2==1: shade(c,"F1F5F6")
borders(t)

doc.add_heading("Objetivo y final", level=1)
doc.add_paragraph("Conquistar la capital rival termina la partida inmediatamente. Si se completan 30 rondas sin que caiga una capital, gana quien tenga más puntos. Cada territorio vale 1 punto, cada tropa 1 punto, cada edificio 2 puntos y cada capital 5 puntos. Si el resultado es igual, hay empate.")

doc.add_heading("Partida directa entre jugadores (P2P)", level=1)
doc.add_paragraph("No hay cuentas ni servidor central. El ordenador de una persona aloja la partida y la otra se conecta directamente. El juego incluye Node.js en el paquete para Windows.")
for text in [
    "El anfitrión entra en Jugar online · P2P y mantiene el juego abierto durante toda la partida.",
    "Selecciona Crear sala y pulsa Iniciar sala. El juego detecta una IPv4 local que puedes editar.",
    "Pulsa Copiar enlace para invitar y compártelo con el rival. El invitado lo abre o lo pega en Unirse con invitación.",
    "Para jugar en la misma Wi-Fi, ambos usan la IPv4 local del anfitrión. Desde redes distintas, el anfitrión necesita permitir TCP 3080 en el firewall y reenviar TCP 3080 en su router. El invitado usa la IP pública del anfitrión.",
    "El código identifica la sala, pero no abre el puerto. CGNAT o las restricciones del proveedor pueden impedir conexiones entrantes. El protocolo actual es HTTP sin cifrar: úsalo solo en una red de confianza."
]: doc.add_paragraph(text, style="List Number")

doc.add_heading("Consejos para la primera partida", level=1)
for text in [
    "Mira el número grande de cada territorio: indica qué tirada puede activar un edificio construido allí. Los edificios de ambos reinos producen con esa suma.",
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

# Matching print-ready PDF version for easy sharing.
navy = colors.HexColor("#18343D"); gold = colors.HexColor("#B27E28"); ink = colors.HexColor("#273137")
styles = getSampleStyleSheet()
styles.add(ParagraphStyle(name="GuideTitle", parent=styles["Title"], fontName="Helvetica-Bold", fontSize=27, leading=31, textColor=colors.black, alignment=TA_LEFT, spaceAfter=2))
styles.add(ParagraphStyle(name="GuideSubtitle", parent=styles["Normal"], fontName="Helvetica-Bold", fontSize=14, leading=18, textColor=gold, spaceAfter=12))
styles.add(ParagraphStyle(name="GuideH1", parent=styles["Heading1"], fontName="Helvetica-Bold", fontSize=16, leading=19, textColor=colors.black, spaceBefore=9, spaceAfter=5))
styles.add(ParagraphStyle(name="GuideBody", parent=styles["BodyText"], fontName="Helvetica", fontSize=9.7, leading=13, textColor=ink, spaceAfter=5))
styles.add(ParagraphStyle(name="GuideStep", parent=styles["BodyText"], fontName="Helvetica", fontSize=9.5, leading=12.5, textColor=ink, leftIndent=5*mm, spaceAfter=7))
styles.add(ParagraphStyle(name="GuideCell", parent=styles["BodyText"], fontName="Helvetica", fontSize=8.5, leading=11, textColor=ink))
styles.add(ParagraphStyle(name="GuideCellHead", parent=styles["BodyText"], fontName="Helvetica-Bold", fontSize=8.7, leading=11, textColor=colors.white))
styles.add(ParagraphStyle(name="GuideFoot", parent=styles["Normal"], fontName="Helvetica", fontSize=8, textColor=colors.HexColor("#647073"), alignment=TA_RIGHT))

story = [Paragraph("Atlas de Aethria", styles["GuideTitle"]), Paragraph("Guía de instalación y juego", styles["GuideSubtitle"]),
         Paragraph("Esta guía explica cómo instalar Atlas de Aethria y jugar en local o en una partida directa entre dos ordenadores. La meta es expandir tu reino, desarrollar sus edificios y conquistar la capital rival.", styles["GuideBody"]),
         Paragraph("Instalar y empezar", styles["GuideH1"])]
for i,text in enumerate([
    "Descarga Atlas-de-Aethria-P2P-Entrega.zip y extrae todo su contenido en una carpeta.",
    "Abre la carpeta AtlasDeAethria y ejecuta Atlas de Aethria.exe. No muevas el .exe fuera de esa carpeta: necesita sus archivos de datos y bibliotecas.",
    "Lee la pantalla Antes de zarpar. Puedes escoger una partida local o una sala online directa P2P.",
    "En local, dos personas juegan por turnos en el mismo ordenador. Para jugar online, sigue la sección Partida directa entre jugadores."
]): story.append(Paragraph(f"{i+1}. {text}", styles["GuideBody"]))
story.append(Paragraph("Cómo funciona un turno", styles["GuideH1"]))
for title,body in steps:
    story.append(KeepTogether([Paragraph(f"<b>{title}</b>", styles["GuideBody"]), Paragraph(body, styles["GuideStep"])]))
story += [PageBreak(), Paragraph("Acciones y costes", styles["GuideH1"]), Paragraph(rules_overview, styles["GuideBody"]), Paragraph("Elige una casilla de tu reino y después la casilla vecina cuando quieras expandirte, mover tropas o atacar. Los botones se desactivan cuando faltan recursos o no se cumplen las condiciones.", styles["GuideBody"])]
data = [[Paragraph(s, styles["GuideCellHead"]) for s in ("Acción", "Coste", "Qué hace")]]
for row in rows: data.append([Paragraph(s, styles["GuideCell"]) for s in row])
table=Table(data, colWidths=[32*mm, 48*mm, 100*mm], repeatRows=1, hAlign="LEFT")
table.setStyle(TableStyle([
    ("BACKGROUND",(0,0),(-1,0),navy),("GRID",(0,0),(-1,-1),.5,colors.HexColor("#D9DEE2")),
    ("ROWBACKGROUNDS",(0,1),(-1,-1),[colors.white,colors.HexColor("#F1F5F6")]),
    ("VALIGN",(0,0),(-1,-1),"MIDDLE"),("LEFTPADDING",(0,0),(-1,-1),7),("RIGHTPADDING",(0,0),(-1,-1),7),
    ("TOPPADDING",(0,0),(-1,-1),7),("BOTTOMPADDING",(0,0),(-1,-1),7)
]))
story += [table, Paragraph("Objetivo y final", styles["GuideH1"]), Paragraph("Conquistar la capital rival termina la partida inmediatamente. Si se completan 30 rondas sin que caiga una capital, gana quien tenga más puntos. Cada territorio vale 1 punto, cada tropa 1 punto, cada edificio 2 puntos y cada capital 5 puntos. Si el resultado es igual, hay empate.", styles["GuideBody"]), Paragraph("Consejos para la primera partida", styles["GuideH1"])]
for text in ["Mira el número grande de cada territorio: indica qué tirada puede activar un edificio construido allí. Los edificios de ambos reinos producen con esa suma.", "El borde dorado señala los territorios activados; el color azul o rojo identifica a quien tiró.", "Compra terrenos variados: cada especialidad produce un recurso distinto y te permite ampliar tus opciones.", "Mantén tropas en el camino hacia la capital rival y deja defensas en tu propio reino."]:
    story.append(Paragraph("• " + text, styles["GuideBody"]))
story += [Paragraph("Controles", styles["GuideH1"]), Paragraph("Ratón: pulsa los botones y las casillas. Para jugar localmente, J1 y J2 comparten el mismo teclado y ratón y se alternan al terminar cada turno.", styles["GuideBody"])]
story += [Paragraph("Partida directa entre jugadores (P2P)", styles["GuideH1"]), Paragraph("No hay cuentas ni servidor central. El anfitrión entra en Jugar online · P2P, selecciona Crear sala y pulsa Iniciar sala. Copia el enlace para invitar y envíalo al rival. El invitado puede abrir el enlace o pegarlo en el juego.", styles["GuideBody"]), Paragraph("En redes distintas, el anfitrión debe permitir TCP 3080 en el firewall y reenviar TCP 3080 en el router hacia su ordenador; el invitado usa la IP pública del anfitrión. El código por sí solo no abre puertos, y CGNAT puede impedir la conexión. La versión actual usa HTTP sin cifrar y está pensada para redes de confianza.", styles["GuideBody"])]
def footer(canvas, document):
    canvas.saveState(); canvas.setFillColor(colors.HexColor("#647073")); canvas.setFont("Helvetica",8)
    canvas.drawRightString(A4[0]-20*mm, 12*mm, f"ATLAS DE AETHRIA  ·  GUÍA RÁPIDA  ·  {document.page}"); canvas.restoreState()
SimpleDocTemplate(PDF_OUT, pagesize=A4, rightMargin=20*mm,leftMargin=20*mm,topMargin=17*mm,bottomMargin=20*mm,title="Atlas de Aethria Guía de instalación y juego").build(story,onFirstPage=footer,onLaterPages=footer)
print(PDF_OUT)


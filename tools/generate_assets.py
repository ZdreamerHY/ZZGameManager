"""Regenerate the bundled icon and offline HTML documents (Python + Pillow)."""
from pathlib import Path
import html
import re
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]


def icon():
    image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((4, 4, 252, 252), radius=52, fill="#101010")
    draw.rounded_rectangle((41, 46, 215, 210), radius=16, fill="#dedede")
    draw.ellipse((157, 66, 190, 99), fill="#ffffff")
    draw.polygon([(41, 174), (109, 90), (194, 210), (41, 210)], fill="#9c9c9c")
    draw.polygon([(85, 210), (163, 131), (215, 176), (215, 210)], fill="#5d5d5d")
    draw.rounded_rectangle((41, 192, 215, 214), radius=8, fill="#2b2b2b")
    image.save(ROOT / "src" / "app.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])


def inline(text):
    # Trusted project Markdown only; protect code spans before other formatting.
    spans = []

    def code(match):
        spans.append("<code>" + html.escape(match.group(1)) + "</code>")
        return f"CODETOKEN{len(spans) - 1}END"

    text = re.sub(r"`([^`]+)`", code, text)
    text = html.escape(text)
    text = re.sub(r"\[([^]]+)\]\((https?://[^)]+)\)", r'<a href="\2">\1</a>', text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
    for index, value in enumerate(spans):
        text = text.replace(f"CODETOKEN{index}END", value)
    return text


def render(source):
    lines = source.splitlines()
    output, toc = [], []
    i = 0
    heading_count = 0
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        if line.startswith("```"):
            block = []
            i += 1
            while i < len(lines) and not lines[i].startswith("```"):
                block.append(lines[i])
                i += 1
            output.append("<pre><code>" + html.escape("\n".join(block)) + "</code></pre>")
        elif re.match(r"^#{1,3} ", line):
            level = len(line.split(" ", 1)[0])
            title = line[level + 1:]
            anchor = f"section-{heading_count}"
            heading_count += 1
            if level == 2:
                toc.append((anchor, title))
            output.append(f'<h{level} id="{anchor}">{inline(title)}</h{level}>')
        elif line.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].startswith("|"):
                cells = lines[i].strip().strip("|").split("|")
                if not all(re.fullmatch(r"\s*:?-+:?\s*", c) for c in cells):
                    rows.append(cells)
                i += 1
            output.append('<div class="table-wrap"><table>')
            for row_index, row in enumerate(rows):
                tag = "th" if row_index == 0 else "td"
                output.append("<tr>" + "".join(f"<{tag}>{inline(c.strip())}</{tag}>" for c in row) + "</tr>")
            output.append("</table></div>")
            continue
        elif re.match(r"^(?:- |\d+\. )", line):
            ordered = not line.startswith("- ")
            tag = "ol" if ordered else "ul"
            pattern = r"^\d+\. " if ordered else r"^- "
            output.append(f"<{tag}>")
            while i < len(lines) and re.match(pattern, lines[i]):
                output.append("<li>" + inline(re.sub(pattern, "", lines[i])) + "</li>")
                i += 1
            output.append(f"</{tag}>")
            continue
        else:
            output.append("<p>" + inline(line) + "</p>")
        i += 1
    return "\n".join(output), toc


CSS = """
:root{color-scheme:light;--ink:#161616;--accent:#161616;--muted:#707070}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#f5f5f5;color:var(--ink);font:15px/1.9 'Microsoft YaHei UI','Microsoft YaHei',system-ui,sans-serif}
.top{background:#101010;color:#ffffff;padding:23px 6vw;font-size:13px;letter-spacing:3px}.layout{max-width:1240px;margin:auto;display:grid;grid-template-columns:240px minmax(0,1fr);gap:48px;padding:48px 32px 80px}
nav{position:sticky;top:24px;align-self:start;font-size:12px}nav b{display:block;color:var(--accent);margin-bottom:16px;letter-spacing:2px}nav a{display:block;color:#606060;text-decoration:none;padding:7px 0;line-height:1.5}nav a:hover{color:var(--accent)}
article{background:#fff;padding:48px 54px;border:1px solid #e4e4e4;border-radius:12px;min-width:0}h1{font-size:30px;line-height:1.5;margin:0 0 16px;letter-spacing:-.5px}h2{font-size:21px;margin:44px 0 16px;padding-top:8px;border-top:1px solid #e7e7e7}h3{font-size:17px;margin-top:30px}p{margin:14px 0}strong{color:#161616}a{color:var(--accent);text-underline-offset:3px}li{margin:7px 0}ul,ol{padding-left:24px}
.table-wrap{overflow-x:auto;margin:22px 0}table{border-collapse:collapse;width:100%;font-size:13px;line-height:1.75}th{text-align:left;background:#eeeeee;color:#292929}th,td{border:1px solid #dddddd;padding:12px 14px;vertical-align:top}tr:nth-child(even) td{background:#fafafa}code{font:12px/1.7 Consolas,monospace;background:#efefef;padding:3px 5px;border-radius:4px;overflow-wrap:anywhere}pre{padding:22px;background:#101010;color:#eeeeee;border-radius:8px;overflow:auto}pre code{background:none;color:inherit;padding:0;white-space:pre}article>p:first-of-type{color:var(--muted);font-size:12px}footer{font-size:12px;color:var(--muted);margin-top:48px;border-top:1px solid #e4e4e4;padding-top:20px}
@media(max-width:850px){.layout{display:block;padding:20px 14px}nav{position:static;margin:16px 10px 28px}nav a{display:inline-block;margin-right:16px}article{padding:28px 22px}h1{font-size:25px}}
@media print{body{background:white;font-size:11pt}.top,nav{display:none}.layout{display:block;padding:0;max-width:none}article{padding:0;border:0}h1{font-size:24pt}h2{font-size:15pt;break-after:avoid}h3{break-after:avoid}tr,pre{break-inside:avoid}a{color:inherit}pre{white-space:pre-wrap;background:#eeeeee;color:#101010}pre code{white-space:pre-wrap}footer{display:none}@page{size:A4;margin:18mm}}
"""


def documents():
    for path in (ROOT / "docs").glob("*.md"):
        body, toc = render(path.read_text(encoding="utf-8"))
        navigation = "".join(f'<a href="#{anchor}">{html.escape(title)}</a>' for anchor, title in toc)
        document = f'''<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{html.escape(path.stem)} · ZZ游戏管理器</title><style>{CSS}</style></head>
<body><div class="top">ZZ / GAME MANAGER　·　LOCAL COLLECTION</div><div class="layout"><nav><b>{html.escape(path.stem)}</b>{navigation}</nav><article>{body}<footer>ZZ游戏管理器 v1.2 · 本地文档 · 可使用浏览器打印或另存为 PDF</footer></article></div></body></html>'''
        path.with_suffix(".html").write_text(document, encoding="utf-8")
        print(path.with_suffix(".html"))


if __name__ == "__main__":
    icon()
    documents()

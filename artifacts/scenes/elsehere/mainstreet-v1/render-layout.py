"""Render the proposed Main Street floor plan. This is a diagram, not a 3D render."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


OUTPUT_DIR = Path(__file__).resolve().parent
image = Image.new("RGB", (1680, 1300), "#F7F4EC")
draw = ImageDraw.Draw(image)
FONT_DIR = Path("C:/Windows/Fonts")
INK = "#293E48"
SCALE = 17


def font(size, bold=False):
    return ImageFont.truetype(str(FONT_DIR / ("malgunbd.ttf" if bold else "malgun.ttf")), size)


def text(x, y, value, size=22, fill=INK, bold=False, anchor=None):
    draw.text((x, y), value, font=font(size, bold), fill=fill, anchor=anchor)


def point(x, z):
    return (100 + (x + 20) * SCALE, 190 + (30 - z) * SCALE)


def area(x1, z1, x2, z2, fill, outline=None, width=1):
    draw.rectangle([point(x1, z2), point(x2, z1)], fill=fill, outline=outline, width=width)


def line(points, fill, width=3):
    draw.line([point(*p) for p in points], fill=fill, width=width, joint="curve")


def dot(x, z, radius, fill, outline=None):
    px, py = point(x, z)
    draw.ellipse((px-radius, py-radius, px+radius, py+radius), fill=fill, outline=outline, width=2)


def label(x, z, value, size=22, fill=INK):
    text(*point(x, z), value, size, fill, bold=True, anchor="mm")


text(90, 42, "elsehere  /  메인스트리트 v1", 40, bold=True)
text(92, 104, "배치 제안 · 브릭 메인스트리트 v3-B × 리틀 멍 v7", 23, "#65746E")
text(100, 151, "평면도  1칸 = 2m  /  북쪽 = +Z", 19)
area(-20, -24, 20, 30, "#DDE5CF", "#A2B092", 2)
for x in range(-20, 21, 2):
    line([(x, -24), (x, 30)], "#CDD7C0", 1)
for z in range(-24, 31, 2):
    line([(-20, z), (20, z)], "#CDD7C0", 1)

# Continuous sidewalks and a six-metre road in both expansion directions.
area(-7, -24, 7, 30, "#E8CFA8")
area(-20, 3, 20, 17, "#E8CFA8")
area(-4, -24, -3, 30, "#CCB794")
area(3, -24, 4, 30, "#CCB794")
area(-20, 6, 20, 7, "#CCB794")
area(-20, 13, 20, 14, "#CCB794")
area(-3, -24, 3, 30, "#697791")
area(-20, 7, 20, 13, "#697791")
for x in [-0.18, 0.18]:
    line([(x, -24), (x, -10.5)], "#EDCD72", 2)
    line([(x, -6.5), (x, 6)], "#EDCD72", 2)
    line([(x, 14), (x, 30)], "#EDCD72", 2)
for x in [-2.6, -1.5, -0.4, 0.7, 1.8]:
    area(x, -10, x + 0.65, -7, "#FFF8E8")

# Exterior footprints; the home porch sits in its two-metre setback.
area(7, -19, 9, -13, "#EBDFCA", "#AFB9B2", 2)
area(9, -19, 15, -13, "#8EAEC0", INK, 2)
area(7, -13, 13, -7, "#F4E8CE", "#AD9F82", 2)
area(-13, -11, -7, -5, "#D97656", INK, 2)
area(-13, -5, -7, 1, "#F2D6A7", INK, 2)
area(7, -5, 15, 3, "#D97656", INK, 2)
area(-15, 17, -7, 25, "#BAC6BC", "#879A90", 2)
area(7, 17, 15, 25, "#BAC6BC", "#879A90", 2)
area(-7, -10.5, -6, -5.5, "#326B57")
area(6, -4.5, 7, -1.5, "#326B57")

# Empty approach rectangles: 2.4m deep by 3m along each facade.
for bounds in [(-7, -10.5, -4.6, -7.5), (4.6, -4.5, 7, -1.5), (6.6, -18.5, 9, -15.5)]:
    area(*bounds, fill=None, outline="#267AB0", width=3)
line([(-7, -10), (-7, -8)], "#FFFFFF", 6)
line([(7, -4), (7, -2)], "#FFFFFF", 6)
line([(9, -18), (9, -16)], "#FFFFFF", 6)

# Minimal curb fixtures; circles mark planting positions, not canopy envelopes.
for x, z in [(-3.5, -18), (-3.5, 0), (3.5, -14), (3.5, 1), (-3.5, 20), (3.5, 22)]:
    dot(x, z, 8, "#659450", "#416A35")
area(11.5, -12.5, 12.7, -11.5, "#8A5D3C")

# Suggested tutorial walking route and hotel branch.
tutorial_route = [(6.5, -17), (6.5, -12), (8.5, -10), (5, -10), (5, -8.5), (-5, -8.5), (-5, -9), (-7, -9)]
line(tutorial_route, "#277CAC", 5)
line([(5, -8.5), (5, -3), (7, -3)], "#AF7149", 4)
dot(6.5, -17, 9, "#FFFFFF", "#277CAC")
dot(8.5, -10, 8, "#277CAC")
dot(10.5, -10, 8, "#E0A044", "#8F642C")
camera = point(5.5, -21)
draw.polygon([(camera[0], camera[1]-12), (camera[0]-10, camera[1]+9), (camera[0]+10, camera[1]+9)], fill="#7657A0")
label(11.8, -16, "01")
label(10, -8, "02", 18)
label(-10, -7, "03", 24, "#FFFFFF")
label(11, -1, "04", 26, "#FFFFFF")
label(-10, -2, "05")
label(-11, 21, "06", 24)
label(11, 21, "06", 24)
label(0, 28, "↑", 29, "#FFFFFF")
label(0, -22, "↓", 29, "#FFFFFF")
label(-17, 10, "←", 29, "#FFFFFF")
label(17, 10, "→", 29, "#FFFFFF")
text(*point(0, 10), "MAPLE AVE", 16, "#FFFFFF", anchor="mm")
for z in [-20, -10, 0, 10, 20, 30]:
    text(84, point(-20, z)[1], f"{z}", 15, "#65746E", anchor="rm")
for x in [-20, -10, 0, 10, 20]:
    text(point(x, -24)[0], 1125, str(x), 15, "#65746E", anchor="mt")
text(95, 1164, "X 40m × Z 54m  /  좌표·길이는 블록아웃 시작값", 20)
draw.line([(100, 1220), (270, 1220)], fill=INK, width=4)
draw.line([(100, 1212), (100, 1228)], fill=INK, width=3)
draw.line([(270, 1212), (270, 1228)], fill=INK, width=3)
text(185, 1230, "10m", 17, anchor="mt")

text(875, 185, "집 → 행인 → 카페 / 호텔", 29, bold=True)
entries = [
    ("01", "집과 현관", "6 × 6m · 높이 4.2m", "HomeScene 연결 · 도착 후 문 앞을 비움"),
    ("02", "첫 대화 앞마당", "6 × 6m · 보행로 밖에 확보", "행인 접근과 대화 · 벤치는 가장자리"),
    ("03", "카페", "6 × 6m · 높이 5.8m", "초록 어닝 · 횡단보도 건너 첫 목적지"),
    ("04", "호텔", "8 × 8m · 높이 5.8m", "세로 간판 · 교차로에서 찾는 방향 표식"),
    ("05", "일반 상점 외관", "6 × 6m · 높이 5.8m", "거리 배경 · 이번 MVP에 내부 기능 추가 없음"),
    ("06", "배경 건물 자리", "공용 외벽 모듈 재사용", "거리 연속감 · 북쪽과 좌우 연결 단면 유지"),
]
for index, (number, title, dimensions, description) in enumerate(entries):
    y = 250 + index * 107
    draw.rounded_rectangle((875, y, 921, y+41), radius=10, fill=INK)
    text(898, y+20, number, 20, "#FFFFFF", True, "mm")
    text(941, y-1, title, 25, bold=True)
    text(941, y+36, dimensions, 20)
    text(941, y+66, description, 19, "#65746E")

draw.line([(875, 916), (1580, 916)], fill="#D1D6CD", width=2)
text(875, 941, "통행과 크기 기준", 27, bold=True)
text(875, 987, "차도 6m  +  양쪽 보도 각 4m", 23)
text(875, 1025, "보도 = 통행 3m + 가구·식재 띠 1m", 21)
text(875, 1063, "캐릭터 1.4m  /  1층 3m  /  문 2 × 2.2m", 21)
text(875, 1110, "파랑: 카페 동선 · 갈색: 호텔 분기", 20, "#277CAC")
text(875, 1146, "파란 테두리: 문 앞 3 × 2.4m 비움 영역", 20)
text(875, 1182, "노랑 점: 행인 · 보라 삼각형: 카메라 후보", 20)
text(875, 1232, "치수 검증은 Unity 블록아웃에서 진행", 19, "#65746E")
image.save(OUTPUT_DIR / "layout-plan.png")
print(OUTPUT_DIR / "layout-plan.png")

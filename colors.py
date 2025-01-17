import matplotlib.colors as mcolors

# Display colors as RGB tuples
named_colors = {}
for name, color in mcolors.CSS4_COLORS.items():
    rgb = mcolors.to_rgb(color)
    named_colors[name] = (rgb[0] * 255, rgb[1] * 255, rgb[2] * 255)

all_colors = list(named_colors.values())

WHITE = (255, 255, 255)
LIGHT_GREY = (200, 200, 200)
GREY = (125, 125, 125)
DARK_GREY = (75, 75, 75)
VERY_DARK_GREY = (25, 25, 25)
RED = (255, 0, 0)
DARK_RED = (100, 0, 0)
BLUE = (0, 0, 255)
GREEN = (0, 255, 0)
BLACK = (0, 0, 0)
DARK_BLUE = (90, 160, 220)
WATER_COLOR = (29, 79, 130)
DARK_WATER_COLOR = (15, 50, 90)
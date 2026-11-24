import sys, glob, math
from PIL import Image, ImageDraw
src, out = sys.argv[1], sys.argv[2]
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 4
files = sorted(glob.glob(src + '/slide*.png'))
tw = (1920 - 10*(cols+1)) // cols; th = tw * 9 // 16
rows = math.ceil(len(files)/cols)
sheet = Image.new('RGB', (1920, rows*(th+34)+10), 'white')
d = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(f).convert('RGB').resize((tw, th))
    x = 10 + (i % cols)*(tw+10); y = 10 + (i//cols)*(th+34)
    sheet.paste(im, (x, y)); d.rectangle([x-1,y-1,x+tw,y+th], outline='#999')
    d.text((x, y+th+6), str(i+1), fill='black')
sheet.save(out); print(out, sheet.size)

param([string]$Portrait = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'website\earth-guardian\assets'
if (-not $Portrait) { $Portrait = Join-Path $assets 'song-yuqiang.jpg' }
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class SiteArtwork {
    static int Clamp(double v) { return (int)Math.Max(0,Math.Min(255,v)); }
    public static void Earth(string source, string output) {
        using(var map = new Bitmap(source))
        using(var globe = new Bitmap(900,900,PixelFormat.Format32bppArgb)) {
            double radius=378, center=450;
            for(int y=60;y<840;y++) for(int x=60;x<840;x++) {
                double nx=(x-center)/radius, ny=(center-y)/radius, r2=nx*nx+ny*ny;
                if(r2>1.05) continue;
                if(r2>1) { double alpha=70*Math.Pow((1.05-r2)/.05,2); globe.SetPixel(x,y,Color.FromArgb(Clamp(alpha),80,206,230)); continue; }
                double nz=Math.Sqrt(1-r2);
                double lat=Math.Asin(ny), lon=Math.Atan2(nx,nz)+100*Math.PI/180;
                int mx=(int)((lon/(2*Math.PI)+.5)*map.Width)%map.Width;
                int my=Math.Min(map.Height-1,(int)((.5-lat/Math.PI)*map.Height));
                Color c=map.GetPixel(mx,my);
                bool land=c.G>c.B*.9;
                double light=Math.Max(0,-nx*.40+ny*.50+nz*.70);
                double rim=Math.Pow(1-nz,5);
                double latitudeGrid=Math.Abs((lat*180/Math.PI+90)%15-7.5);
                double longitudeGrid=Math.Abs((lon*180/Math.PI+180)%15-7.5);
                double grid=(latitudeGrid<.18||longitudeGrid<.18)?13:0;
                double grain=((x*37+y*17)%13)/13.0;
                double d=.2+.8*light;
                double rr=(land?42:5)*d+rim*57+grid;
                double gg=(land?156:49)*d+rim*123+grid;
                double bb=(land?148:80)*d+rim*153+grid;
                if(land && x%5==0 && y%5==0) { rr+=42*d;gg+=73*d;bb+=56*d; }
                if(land && ((x*71+y*313)%439<2)) { rr+=100*d;gg+=110*d;bb+=70*d; }
                globe.SetPixel(x,y,Color.FromArgb(255,Clamp(rr+grain*3),Clamp(gg+grain*3),Clamp(bb+grain*3)));
            }
            using(var g=Graphics.FromImage(globe)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var pen=new Pen(Color.FromArgb(120,123,237,240),1.6f)) g.DrawEllipse(pen,72,72,756,756);
            }
            globe.Save(output,ImageFormat.Png);
        }
    }
    static void Text(Graphics g,string text,int size,float x,float y,Color color,FontStyle style=FontStyle.Regular) {
        using(var font=new Font("Microsoft YaHei UI",size,style,GraphicsUnit.Pixel))
        using(var brush=new SolidBrush(color)) g.DrawString(text,font,brush,x,y);
    }
    public static void Poster(string photo,string earth,string output) {
        using(var bitmap=new Bitmap(1200,1500))
        using(var g=Graphics.FromImage(bitmap))
        using(var portrait=Image.FromFile(photo))
        using(var globe=Image.FromFile(earth)) {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.FromArgb(10,27,31));
            g.DrawImage(portrait,new Rectangle(0,380,1200,1200));
            using(var gradient=new LinearGradientBrush(new Rectangle(0,370,1200,235),Color.FromArgb(255,10,27,31),Color.FromArgb(0,10,27,31),90f)) g.FillRectangle(gradient,0,370,1200,235);
            using(var gradient=new LinearGradientBrush(new Rectangle(0,1190,1200,310),Color.FromArgb(0,8,30,30),Color.FromArgb(245,8,30,30),90f)) g.FillRectangle(gradient,0,1190,1200,310);
            using(var pen=new Pen(Color.FromArgb(60,129,207,186),1)) {g.DrawEllipse(pen,817,-113,520,520);g.DrawEllipse(pen,845,-85,464,464);g.DrawLine(pen,70,1340,1130,1340);}
            g.DrawImage(globe,new Rectangle(848,7,300,300));
            Color mint=Color.FromArgb(149,245,200), white=Color.FromArgb(233,246,239);
            Text(g,"A MAKER'S NOTE",20,74,65,mint);
            Text(g,"从一个小问题开始。",65,68,125,white,FontStyle.Bold);
            Text(g,"让想法，落在桌面上。",48,71,224,Color.FromArgb(164,197,186));
            Text(g,"宋域强  /  开发者",26,74,323,mint);
            Text(g,"公众号 · 抖音",23,74,1373,Color.FromArgb(174,208,191));
            Text(g,"宋域强",46,72,1410,white,FontStyle.Bold);
            Text(g,"BUILD. LEARN. SHARE.",18,818,1443,mint);
            bitmap.Save(output,ImageFormat.Jpeg);
        }
    }
}
'@
[SiteArtwork]::Earth((Join-Path $root 'orb\Assets\earth-map.png'), (Join-Path $assets 'earth.png'))
[SiteArtwork]::Poster($Portrait, (Join-Path $assets 'earth.png'), (Join-Path $assets 'developer-poster.jpg'))
Write-Output "Created artwork in $assets"

using System.Drawing;

namespace CJFingerService;

internal static class WineLoginVision {
    // Only accept a bounded, light-filled input rectangle below the OCR label.
    // Do not infer an input from a fixed screen coordinate or Tab order.
    internal static Rectangle FindField(Bitmap image,Point label) {
        var found=new List<Rectangle>();
        for(var y=label.Y+5;y<Math.Min(image.Height-15,label.Y+45);y++) {
            var start=-1;
            for(var x=Math.Max(0,label.X-160);x<image.Width;x++) {
                var pixel=image.GetPixel(x,y);
                var border=Math.Min(pixel.R,Math.Min(pixel.G,pixel.B))<230;
                if(border && start<0)start=x;
                if(border || start<0)continue;
                var left=start;start=-1;var width=x-left;
                if(width<100 || width>image.Width*.7 || label.X<left-15 || label.X>left+width)continue;
                for(var height=16;height<=55 && y+height<image.Height;height++) {
                    var bottom=0;var white=0;var total=0;
                    for(var col=left+4;col<x-4;col+=3) {
                        var p=image.GetPixel(col,y+height);
                        if(Math.Min(p.R,Math.Min(p.G,p.B))<230)bottom++;
                        for(var row=y+4;row<y+height-3;row+=3) {
                            p=image.GetPixel(col,row);total++;
                            if(p.R>235 && p.G>235 && p.B>235)white++;
                        }
                    }
                    if(bottom<(width-8)/3*.8 || total==0 || white<total*.85)continue;
                    bool HasSide(int edge) {
                        for(var col=Math.Max(0,edge-6);col<=Math.Min(image.Width-1,edge+6);col++) {
                            var dark=0;var samples=0;
                            for(var row=y+4;row<y+height-3;row++) {
                                var p=image.GetPixel(col,row);samples++;
                                if(Math.Min(p.R,Math.Min(p.G,p.B))<230)dark++;
                            }
                            if(samples>0 && dark>=samples*.8)return true;
                        }
                        return false;
                    }
                    if(!HasSide(left) || !HasSide(x-1))continue;
                    var rect=new Rectangle(left,y,width,height);
                    if(!found.Any(r=>r.IntersectsWith(rect)))found.Add(rect);
                    break;
                }
            }
        }
        if(found.Count!=1)throw new InvalidOperationException("Wine OCR cannot uniquely identify the bordered login input. Check font rendering and window size.");
        return found[0];
    }
    internal static int MaskedCharacterCount(Bitmap image,Rectangle field) {
        var inside=Rectangle.Inflate(field,-4,-4);
        if(!new Rectangle(0,0,image.Width,image.Height).Contains(inside))return -1;
        var count=0;var start=-1;var top=int.MaxValue;var bottom=0;
        for(var x=inside.Left;x<=inside.Right;x++) {
            var dark=false;
            if(x<inside.Right)for(var y=inside.Top;y<inside.Bottom;y++) {
                var p=image.GetPixel(x,y);
                if(p.R<120 && p.G<120 && p.B<120){dark=true;top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            }
            if(dark){if(start<0)start=x;continue;}
            if(start<0)continue;
            var width=x-start;var height=bottom-top+1;
            if(width>1) {
                if(height<2 || width>height*2 || height>width*2)return -1;
                count++;
            }
            start=-1;top=int.MaxValue;bottom=0;
        }
        return count;
    }
}

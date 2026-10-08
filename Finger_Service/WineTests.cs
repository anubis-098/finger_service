using System.Drawing;

namespace CJFingerService;
internal static class WineTests {
    internal static void Run() {
        using var image=new Bitmap(640,400);
        using(var g=Graphics.FromImage(image)) {
            g.Clear(Color.White);
            g.DrawRectangle(Pens.Gray,220,120,190,24);
            g.DrawRectangle(Pens.Gray,220,175,190,24);
            g.FillRectangle(Brushes.SteelBlue,220,210,190,25);
        }
        var user=WineLoginVision.FindField(image,new(250,110));
        var password=WineLoginVision.FindField(image,new(250,165));
        if(!user.Contains(250,130) || !password.Contains(250,185) || user.IntersectsWith(password))throw new Exception("Wine OCR selected the wrong input rectangle.");
        using(var g=Graphics.FromImage(image))for(var i=0;i<9;i++)g.FillEllipse(Brushes.Black,230+i*10,183,5,5);
        if(WineLoginVision.MaskedCharacterCount(image,password)!=9)throw new Exception("Wine password mask count mismatch.");
        using(var empty=new Bitmap(640,400)) {
            using(var g=Graphics.FromImage(empty))g.Clear(Color.White);
            var rejected=false;try{WineLoginVision.FindField(empty,new(250,110));}catch(InvalidOperationException){rejected=true;}
            if(!rejected)throw new Exception("Wine accepted an input without a visible border.");
        }
        var sample=Environment.GetEnvironmentVariable("CJ_FINGER_LOGIN_SAMPLE");
        if(!string.IsNullOrEmpty(sample)) {
            using var screenshot=new Bitmap(sample);
            var first=WineLoginVision.FindField(screenshot,new(252,110));
            var second=WineLoginVision.FindField(screenshot,new(251,160));
            if(!first.Contains(300,132) || !second.Contains(300,181) || first.IntersectsWith(second))throw new Exception("WEB8 reference screenshot inputs were not identified correctly.");
        }
        File.WriteAllText(Path.Combine(Settings.Root,"wine-test-result.txt"),"PASS: bordered login fields, missing-field rejection, password mask count"+(sample is null?"":" and real WEB8 reference image")+".");
    }
}

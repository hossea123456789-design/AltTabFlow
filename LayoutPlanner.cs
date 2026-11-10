namespace AltTabFlow;

internal sealed record ListLayout(int Columns,int Rows,int Pitch,float Width,float Height,bool Preview)
{
    public int Capacity=>Columns*Rows;
}
internal sealed record TileLayout(int Columns,int Rows,float Width,float Height){public int Capacity=>Columns*Rows;}

internal static class LayoutPlanner
{
    public static TileLayout PlanTiles(int count,float availableWidth,float availableHeight,bool icons) {
        count=Math.Max(1,count);int pitch=icons?158:216;
        int maxCols=Math.Max(1,(int)((availableWidth-44)/(icons?162:272)));
        int maxRows=Math.Max(1,(int)((Math.Min(1400,availableHeight)-300)/pitch));
        if(count>maxCols*maxRows)maxRows=Math.Max(1,(int)((availableHeight-300)/pitch));
        int preferred=icons?5:Math.Max(3,(int)Math.Ceiling(Math.Sqrt(count)));
        int cols=Math.Clamp(Math.Max(preferred,(count+maxRows-1)/maxRows),1,maxCols);
        int rows=Math.Min(maxRows,(count+cols-1)/cols);
        float width=Math.Min(availableWidth,Math.Max(900,56+cols*(icons?190:300)+(cols-1)*12));
        float height=Math.Min(availableHeight,Math.Max(590,300+rows*pitch));
        return new(cols,rows,width,height);
    }
    public static ListLayout Plan(int count,float availableWidth,float availableHeight,bool preview) {
        count=Math.Max(1,count);
        float maxWidth=Math.Max(500,availableWidth),maxHeight=Math.Max(450,availableHeight);
        int pitch=74;
        int maxColumns=Math.Max(1,(int)((maxWidth-56)/330));
        int maxRows=Math.Max(1,(int)((Math.Min(1100,maxHeight)-300)/pitch));
        if(count>maxColumns*maxRows)maxRows=Math.Max(1,(int)((maxHeight-300)/pitch));
        if(count>maxColumns*maxRows) {pitch=62;maxRows=Math.Max(1,(int)((maxHeight-300)/pitch));}
        if(count>maxColumns*maxRows)maxColumns=Math.Max(1,(int)((maxWidth-56)/250));
        int columns=Math.Clamp((count+maxRows-1)/maxRows,1,maxColumns);
        int rows=Math.Min(maxRows,(count+columns-1)/columns);
        float ideal=56+columns*510+(columns-1)*14;
        bool showPreview=preview&&ideal+400<=maxWidth;
        float width=Math.Min(maxWidth,Math.Max(900,ideal+(showPreview?400:0)));
        float height=Math.Min(maxHeight,Math.Max(590,300+rows*pitch));
        return new(columns,rows,pitch,width,height,showPreview);
    }
}

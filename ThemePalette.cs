namespace AltTabFlow;

internal sealed record ThemePalette(string Id,string Name,bool Light,Color Bg,Color Surface,Color Raised,Color Ink,Color Muted,Color Accent,Color Selected)
{
    private static Color Hex(int rgb)=>Color.FromArgb((rgb>>16)&255,(rgb>>8)&255,rgb&255);
    private static ThemePalette Dark(string id,string name,int bg,int surface,int raised,int accent,int selected)=>
        new(id,name,false,Hex(bg),Hex(surface),Hex(raised),Hex(0xEDF3FB),Hex(0xA6B5C9),Hex(accent),Hex(selected));
    public static readonly ThemePalette[] All={
        Dark("mint","민트 다크",0x12161F,0x1C222E,0x242B39,0x57E0C1,0x1D3939),
        Dark("ocean","오션 블루",0x101A2B,0x19283E,0x22344C,0x75B9FF,0x20415E),
        Dark("violet","바이올렛",0x1C172B,0x292238,0x352D46,0xC4A2FF,0x453259),
        Dark("rose","로즈",0x25181F,0x34242F,0x44303D,0xFFA4C0,0x533246),
        Dark("amber","앰버",0x211D16,0x30291F,0x403629,0xF4CD7C,0x514127),
        Dark("nord","노르딕",0x2E3440,0x3B4252,0x434C5E,0x88C0D0,0x3E5668),
        new("light","클라우드 라이트",true,Hex(0xF4F7FB),Color.White,Hex(0xE8EDF5),Hex(0x192434),Hex(0x53647B),Hex(0x006B65),Hex(0xDAF0E9))
    };
    public static ThemePalette Resolve(Preferences p)=>All.FirstOrDefault(x=>x.Id==p.ColorPreset)??All[p.LightTheme?6:0];
    public override string ToString()=>Name;
}

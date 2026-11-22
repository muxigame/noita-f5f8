using System;
using System.IO;
using System.Runtime.InteropServices;

class BootstrapHarness
{
    const string LuaDll = @"C:\Program Files (x86)\Steam\steamapps\common\Noita\lua51.dll";
    [DllImport(LuaDll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr luaL_newstate();
    [DllImport(LuaDll, CallingConvention=CallingConvention.Cdecl)] static extern void luaL_openlibs(IntPtr state);
    [DllImport(LuaDll, CallingConvention=CallingConvention.Cdecl)] static extern int luaL_loadfile(IntPtr state, string file);
    [DllImport(LuaDll, CallingConvention=CallingConvention.Cdecl)] static extern int luaL_loadstring(IntPtr state, string source);
    [DllImport(LuaDll, CallingConvention=CallingConvention.Cdecl)] static extern int lua_pcall(IntPtr state, int arguments, int results, int errorFunction);
    [DllImport(LuaDll, CallingConvention=CallingConvention.Cdecl)] static extern IntPtr lua_tolstring(IntPtr state, int index, out UIntPtr length);
    [DllImport(LuaDll, CallingConvention=CallingConvention.Cdecl)] static extern void lua_close(IntPtr state);
    static void Check(IntPtr state, int result)
    {
        if (result == 0) return;
        UIntPtr length;
        throw new Exception(Marshal.PtrToStringAnsi(lua_tolstring(state, -1, out length)));
    }
    static int Main(string[] args)
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string profile = Path.Combine(root, "profile");
        Directory.CreateDirectory(profile);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", profile);
        Environment.SetEnvironmentVariable("NFS_TEST_OUTPUT", Path.Combine(root, "child-arguments.txt"));
        IntPtr state = luaL_newstate();
        try
        {
            luaL_openlibs(state);
            Check(state, luaL_loadstring(state, "GamePrintImportant=function(a,b) error(a..': '..b) end"));
            Check(state, lua_pcall(state, 0, 0, 0));
            Check(state, luaL_loadfile(state, args[0]));
            Check(state, lua_pcall(state, 0, 0, 0));
            Console.WriteLine("BOOTSTRAP_EXECUTED");
            Check(state, luaL_loadstring(state, @"
local players, alive, controls, enabled = {}, false, nil, false
local flags = {}
EntityGetWithTag = function(tag) assert(tag == 'player_unit'); return players end
EntityGetIsAlive = function(player) assert(player == 11); return alive end
EntityGetFirstComponentIncludingDisabled = function(player, name)
    assert(player == 11 and name == 'ControlsComponent'); return controls
end
ComponentGetIsEnabled = function(component) assert(component == 55); return enabled end
RemoveFlagPersistent = function(flag) flags[#flags + 1] = 'remove:' .. flag end
AddFlagPersistent = function(flag) flags[#flags + 1] = 'add:' .. flag end
OnWorldPostUpdate(); assert(#flags == 0)
players = {11}; OnWorldPostUpdate(); assert(#flags == 0)
alive = true; controls = 0; OnWorldPostUpdate(); assert(#flags == 0)
controls = 55; OnWorldPostUpdate(); assert(#flags == 0)
enabled = true; OnWorldPostUpdate(); assert(#flags == 2)
assert(flags[1] == 'remove:noita_f5f8_gameplay_ready')
assert(flags[2] == 'add:noita_f5f8_gameplay_ready')
OnWorldPostUpdate(); assert(#flags == 2)
"));
            Check(state, lua_pcall(state, 0, 0, 0));
            Console.WriteLine("READINESS_REGRESSION_OK");
            return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
        finally { lua_close(state); }
    }
}

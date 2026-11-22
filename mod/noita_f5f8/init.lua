-- This package intentionally requests full APIs and runs ONLY as a local mod.
-- Copy the complete Workshop folder 3835902155 to <Noita installation>/mods/.
-- The helper is started silently from this initialization context, not from
-- settings.lua or per-frame scripts. No Steam launch options are necessary.

local ready = false
local ready_flag = "noita_f5f8_gameplay_ready"

function OnWorldPostUpdate()
    if ready then return end
    for _, player in ipairs(EntityGetWithTag("player_unit") or {}) do
        local controls = EntityGetFirstComponentIncludingDisabled(player, "ControlsComponent")
        if EntityGetIsAlive(player) and controls ~= nil and controls ~= 0
            and ComponentGetIsEnabled(controls) then
            RemoveFlagPersistent(ready_flag)
            AddFlagPersistent(ready_flag)
            ready = true
            return
        end
    end
end

local function bootstrap()
    local ffi = require("ffi")
    -- Unique structure names avoid collisions with other local unsafe mods.
    if not pcall(ffi.typeof, "NFS_STARTUPINFOW") then
        ffi.cdef[[
            typedef struct {
                uint32_t cb;
                uint16_t *lpReserved, *lpDesktop, *lpTitle;
                uint32_t dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
                uint32_t dwFillAttribute, dwFlags;
                uint16_t wShowWindow, cbReserved2;
                uint8_t *lpReserved2;
                void *hStdInput, *hStdOutput, *hStdError;
            } NFS_STARTUPINFOW;
            typedef struct {
                void *hProcess, *hThread;
                uint32_t dwProcessId, dwThreadId;
            } NFS_PROCESS_INFORMATION;
        ]]
    end
    -- Existing compatible declarations from other mods are harmless.
    pcall(ffi.cdef, "void* __stdcall GetModuleHandleA(const char*);")
    pcall(ffi.cdef, "void* __stdcall GetProcAddress(void*, const char*);")
    local kernel = ffi.C.GetModuleHandleA("kernel32.dll")
    assert(kernel ~= nil, "kernel32.dll is unavailable")
    local function proc(module, name, signature)
        local address = ffi.C.GetProcAddress(module, name)
        assert(address ~= nil, "Missing Windows function: " .. name)
        return ffi.cast(signature, address)
    end

    local to_wide = proc(kernel, "MultiByteToWideChar", "int32_t (__stdcall*)(uint32_t,uint32_t,const char*,int32_t,uint16_t*,int32_t)")
    local to_utf8 = proc(kernel, "WideCharToMultiByte", "int32_t (__stdcall*)(uint32_t,uint32_t,const uint16_t*,int32_t,char*,int32_t,const char*,int32_t*)")
    local last_error = proc(kernel, "GetLastError", "uint32_t (__stdcall*)()")
    local function wide(text)
        local length = to_wide(65001, 8, text, #text, nil, 0)
        assert(length > 0, "Invalid UTF-8 path")
        local result = ffi.new("uint16_t[?]", length + 1)
        assert(to_wide(65001, 8, text, #text, result, length) == length, "Cannot encode Windows path")
        return result
    end
    local function utf8(buffer, length)
        local bytes = to_utf8(65001, 0, buffer, length, nil, 0, nil, nil)
        assert(bytes > 0, "Cannot read Windows path")
        local result = ffi.new("char[?]", bytes)
        assert(to_utf8(65001, 0, buffer, length, result, bytes, nil, nil) == bytes, "Cannot decode Windows path")
        return ffi.string(result, bytes)
    end
    local module_filename = proc(kernel, "GetModuleFileNameW", "uint32_t (__stdcall*)(void*,uint16_t*,uint32_t)")
    local environment = proc(kernel, "GetEnvironmentVariableW", "uint32_t (__stdcall*)(const uint16_t*,uint16_t*,uint32_t)")
    local attributes = proc(kernel, "GetFileAttributesW", "uint32_t (__stdcall*)(const uint16_t*)")
    local mkdir = proc(kernel, "CreateDirectoryW", "int32_t (__stdcall*)(const uint16_t*,void*)")
    local create_file = proc(kernel, "CreateFileW", "void* (__stdcall*)(const uint16_t*,uint32_t,uint32_t,void*,uint32_t,uint32_t,void*)")
    local file_size = proc(kernel, "GetFileSizeEx", "int32_t (__stdcall*)(void*,int64_t*)")
    local read_file = proc(kernel, "ReadFile", "int32_t (__stdcall*)(void*,void*,uint32_t,uint32_t*,void*)")
    local write_file = proc(kernel, "WriteFile", "int32_t (__stdcall*)(void*,const void*,uint32_t,uint32_t*,void*)")
    local flush = proc(kernel, "FlushFileBuffers", "int32_t (__stdcall*)(void*)")
    local move_file = proc(kernel, "MoveFileExW", "int32_t (__stdcall*)(const uint16_t*,const uint16_t*,uint32_t)")
    local delete_file = proc(kernel, "DeleteFileW", "int32_t (__stdcall*)(const uint16_t*)")
    local close_handle = proc(kernel, "CloseHandle", "int32_t (__stdcall*)(void*)")
    local process_id = proc(kernel, "GetCurrentProcessId", "uint32_t (__stdcall*)()")
    local invalid_handle = ffi.cast("void*", -1)
    local function failure(message) error(message .. " (Windows error " .. tonumber(last_error()) .. ")", 0) end
    local function attribute(path)
        local value = tonumber(attributes(wide(path)))
        if value == 4294967295 then return nil end
        return value
    end
    local function no_reparse(path)
        local current = path:gsub("/", "\\"):gsub("\\+$", "")
        while current ~= "" and not current:match("^[A-Za-z]:$") do
            local value = attribute(current)
            assert(value == nil or bit.band(value, 0x400) == 0, "Symbolic links/junctions are not supported: " .. current)
            local parent = current:match("^(.*)\\[^\\]+$")
            if parent == nil or parent == current then break end
            current = parent
        end
    end
    local function read_all(path)
        no_reparse(path)
        local handle = create_file(wide(path), 0x80000000, 1, nil, 3, 0x80, nil)
        if handle == invalid_handle then return nil end
        local ok, result = pcall(function()
            local length = ffi.new("int64_t[1]")
            if file_size(handle, length) == 0 then failure("Cannot read file size") end
            local size = tonumber(length[0])
            assert(size >= 0 and size <= 16 * 1024 * 1024, "Unexpected companion file size")
            if size == 0 then return "" end
            local buffer = ffi.new("uint8_t[?]", size)
            local count = ffi.new("uint32_t[1]")
            if read_file(handle, buffer, size, count, nil) == 0 or tonumber(count[0]) ~= size then failure("Cannot read complete file") end
            return ffi.string(buffer, size)
        end)
        close_handle(handle)
        if not ok then error(result, 0) end
        return result
    end

    local game_buffer = ffi.new("uint16_t[32768]")
    local game_length = tonumber(module_filename(nil, game_buffer, 32768))
    assert(game_length > 0 and game_length < 32767, "Cannot locate the running Noita executable")
    local game_exe = utf8(game_buffer, game_length)
    assert(game_exe:lower():match("[\\/]noita%.exe$") or game_exe:lower():match("[\\/]noita_dev%.exe$"), "Unexpected running executable")
    local game_dir = assert(game_exe:match("^(.*)[\\/][^\\/]+$"))
    local mod_dir = game_dir .. "\\mods\\3835902155"
    local payload = assert(read_all(mod_dir .. "\\companion\\NoitaF5F8.bin"), "Copy the complete 3835902155 folder into the game's mods directory first")
    assert(payload:sub(1, 2) == "MZ", "The bundled Windows companion is invalid")
    local manifest = assert(read_all(mod_dir .. "\\companion\\SHA256.txt"), "The bundled SHA256 manifest is missing")
    local expected = assert(manifest:match("^([A-Fa-f0-9]+)"), "Invalid bundled SHA256 manifest"):lower()
    assert(#expected == 64, "Invalid SHA256 length")

    -- Verify the bundled payload before creating an executable. BCrypt is the
    -- operating system's SHA256 implementation, not an additional dependency.
    local load_library = proc(kernel, "LoadLibraryW", "void* (__stdcall*)(const uint16_t*)")
    local free_library = proc(kernel, "FreeLibrary", "int32_t (__stdcall*)(void*)")
    local system_directory = proc(kernel, "GetSystemDirectoryW", "uint32_t (__stdcall*)(uint16_t*,uint32_t)")
    local system_buffer = ffi.new("uint16_t[32768]")
    local system_length = tonumber(system_directory(system_buffer, 32768))
    assert(system_length > 0 and system_length < 32768, "Cannot locate Windows system directory")
    local crypto_module = load_library(wide(utf8(system_buffer, system_length) .. "\\bcrypt.dll"))
    assert(crypto_module ~= nil, "Windows SHA256 is unavailable")
    local open_algorithm = proc(crypto_module, "BCryptOpenAlgorithmProvider", "int32_t (__stdcall*)(void**,const uint16_t*,const uint16_t*,uint32_t)")
    local close_algorithm = proc(crypto_module, "BCryptCloseAlgorithmProvider", "int32_t (__stdcall*)(void*,uint32_t)")
    local get_property = proc(crypto_module, "BCryptGetProperty", "int32_t (__stdcall*)(void*,const uint16_t*,uint8_t*,uint32_t,uint32_t*,uint32_t)")
    local create_hash = proc(crypto_module, "BCryptCreateHash", "int32_t (__stdcall*)(void*,void**,uint8_t*,uint32_t,uint8_t*,uint32_t,uint32_t)")
    local hash_data = proc(crypto_module, "BCryptHashData", "int32_t (__stdcall*)(void*,const uint8_t*,uint32_t,uint32_t)")
    local finish_hash = proc(crypto_module, "BCryptFinishHash", "int32_t (__stdcall*)(void*,uint8_t*,uint32_t,uint32_t)")
    local destroy_hash = proc(crypto_module, "BCryptDestroyHash", "int32_t (__stdcall*)(void*)")
    local algorithm, hash = ffi.new("void*[1]"), ffi.new("void*[1]")
    local hash_ok, actual = pcall(function()
        assert(open_algorithm(algorithm, wide("SHA256"), nil, 0) == 0, "Cannot initialize SHA256")
        local size, written = ffi.new("uint32_t[1]"), ffi.new("uint32_t[1]")
        assert(get_property(algorithm[0], wide("ObjectLength"), ffi.cast("uint8_t*", size), 4, written, 0) == 0, "Cannot initialize SHA256 storage")
        local object = ffi.new("uint8_t[?]", tonumber(size[0]))
        assert(create_hash(algorithm[0], hash, object, size[0], nil, 0, 0) == 0, "Cannot create SHA256 hash")
        assert(hash_data(hash[0], ffi.cast("const uint8_t*", payload), #payload, 0) == 0, "Cannot hash companion")
        local digest = ffi.new("uint8_t[32]")
        assert(finish_hash(hash[0], digest, 32, 0) == 0, "Cannot finish companion hash")
        local result = {}
        for i = 0, 31 do result[#result + 1] = string.format("%02x", tonumber(digest[i])) end
        return table.concat(result)
    end)
    if hash[0] ~= nil then destroy_hash(hash[0]) end
    if algorithm[0] ~= nil then close_algorithm(algorithm[0], 0) end
    free_library(crypto_module)
    if not hash_ok then error(actual, 0) end
    assert(actual == expected, "Companion SHA256 verification failed; copy the complete Workshop folder again")

    local appdata_buffer = ffi.new("uint16_t[32768]")
    local appdata_length = tonumber(environment(wide("LOCALAPPDATA"), appdata_buffer, 32768))
    assert(appdata_length > 0 and appdata_length < 32768, "LOCALAPPDATA is unavailable")
    local install_dir = utf8(appdata_buffer, appdata_length) .. "\\NoitaF5F8"
    no_reparse(install_dir)
    if attribute(install_dir) == nil and mkdir(wide(install_dir), nil) == 0 and tonumber(last_error()) ~= 183 then failure("Cannot create the companion directory") end
    no_reparse(install_dir)
    local helper = install_dir .. "\\local-helper-" .. expected:sub(1, 16) .. ".exe"
    if read_all(helper) ~= payload then
        local temporary = helper .. ".tmp-" .. tonumber(process_id())
        no_reparse(temporary)
        local file = create_file(wide(temporary), 0x40000000, 0, nil, 2, 0x80, nil)
        if file == invalid_handle then failure("Cannot extract the companion") end
        local write_ok, write_error = pcall(function()
            local count = ffi.new("uint32_t[1]")
            if write_file(file, ffi.cast("const void*", payload), #payload, count, nil) == 0 or tonumber(count[0]) ~= #payload then failure("Cannot write the complete companion") end
            if flush(file) == 0 then failure("Cannot flush the companion") end
        end)
        close_handle(file)
        if not write_ok then delete_file(wide(temporary)); error(write_error, 0) end
        assert(read_all(temporary) == payload, "Extracted companion verification failed")
        no_reparse(helper)
        if move_file(wide(temporary), wide(helper), 1 + 8) == 0 then delete_file(wide(temporary)); failure("Cannot install the companion") end
    end

    local create_process = proc(kernel, "CreateProcessW", "int32_t (__stdcall*)(const uint16_t*,uint16_t*,void*,void*,int32_t,uint32_t,void*,const uint16_t*,NFS_STARTUPINFOW*,NFS_PROCESS_INFORMATION*)")
    local startup, process = ffi.new("NFS_STARTUPINFOW"), ffi.new("NFS_PROCESS_INFORMATION")
    startup.cb = ffi.sizeof(startup)
    startup.dwFlags, startup.wShowWindow = 1, 0 -- STARTF_USESHOWWINDOW / SW_HIDE
    local command = '"' .. helper .. '" --launch-game "' .. game_exe .. '"'
    if create_process(wide(helper), wide(command), nil, nil, 0, 0x08000000, nil, wide(game_dir), startup, process) == 0 then failure("Cannot start the companion") end
    close_handle(process.hThread)
    close_handle(process.hProcess)
end

local ok, message = pcall(bootstrap)
if not ok then
    local text = tostring(message)
    print("[Noita F5/F8] Local bootstrap failed: " .. text)
    if GamePrintImportant ~= nil then GamePrintImportant("F5/F8 本地模组启动失败", text) end
end

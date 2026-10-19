import ctypes
import ctypes.wintypes
import psutil

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

def enum_thread_proc(hwnd, lparam):
    length = user32.GetWindowTextLengthW(hwnd)
    buff = ctypes.create_unicode_buffer(length + 1)
    user32.GetWindowTextW(hwnd, buff, length + 1)
    print(f"HWND={hwnd}, Title='{buff.value}', Visible={user32.IsWindowVisible(hwnd)}")
    # Restore and bring to foreground
    user32.ShowWindow(hwnd, 9)
    user32.SetForegroundWindow(hwnd)
    return True

EnumThreadWndProc = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.wintypes.HWND, ctypes.c_void_p)
callback = EnumThreadWndProc(enum_thread_proc)

try:
    p = psutil.Process(6572)
    threads = p.threads()
    print(f"Process 6572 has {len(threads)} threads.")
    for t in threads:
        user32.EnumThreadWindows(t.id, callback, 0)
except Exception as e:
    print("Error:", e)

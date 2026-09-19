#!/usr/bin/env python3
"""TERMINAL HELL - Linux smoke test in a pseudo terminal (run by CI on Linux).

Runs the game the way a terminal would: answers its start-up questions (as a plain terminal and as a kitty
protocol terminal), sends keys and mouse reports, and checks that it draws, quits when asked (autotest end,
Ctrl+C, SIGTERM) and always leaves the terminal settings exactly as it found them.

    python3 linux/tests/pty_smoke.py path/to/terminalhell
"""
import fcntl
import os
import select
import signal
import struct
import sys
import tempfile
import termios
import time

EXE = os.path.abspath(sys.argv[1])
PLAIN = b"\x1b[?1004;2$y\x1b[?1016;0$y\x1b[?62;22c"                 # focus reports, no pixel mouse, no kitty
KITTY = b"\x1b[?11u\x1b[?1004;2$y\x1b[?1016;2$y\x1b[?62;22c"       # kitty flags 11, focus, pixel mouse
failures = []


def check(ok, what):
    print(("  ok    " if ok else "  FAIL  ") + what)
    if not ok:
        failures.append(what)


def run(args, answers, keys=(), kill=None, timeout=60):
    """Starts the game on a fresh pty (120x40 cells, 9x18 pixel cells).
    answers: bytes sent once the game has asked its questions (it ends them with CSI c).
    keys: [(seconds after the answers, bytes)].  kill: (seconds after the answers, signal).
    Returns (exit code or None on timeout, everything it wrote, pty settings before, after)."""
    master, slave = os.openpty()
    fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", 40, 120, 1080, 720))
    before = termios.tcgetattr(slave)
    pid = os.fork()
    if pid == 0:
        try:
            os.setsid()
            fcntl.ioctl(slave, termios.TIOCSCTTY, 0)
            for fd in (0, 1, 2):
                os.dup2(slave, fd)
            os.close(master)
            if slave > 2:
                os.close(slave)
            os.execve(EXE, [EXE] + args, dict(os.environ, TERM="xterm-256color"))
        finally:
            os._exit(127)

    out = bytearray()
    asked_at = None
    pending = list(keys)
    status = None
    deadline = time.time() + timeout
    while time.time() < deadline:
        ready, _, _ = select.select([master], [], [], 0.05)
        if master in ready:
            try:
                out += os.read(master, 65536)
            except OSError:
                pass
        if asked_at is None and b"\x1b[c" in out:
            os.write(master, answers)
            asked_at = time.time()
        if asked_at is not None:
            while pending and time.time() - asked_at >= pending[0][0]:
                os.write(master, pending.pop(0)[1])
            if kill is not None and time.time() - asked_at >= kill[0]:
                os.kill(pid, kill[1])
                kill = None
        done, st = os.waitpid(pid, os.WNOHANG)
        if done:
            status = st
            break
    code = None
    if status is None:
        os.kill(pid, signal.SIGKILL)
        os.waitpid(pid, 0)
    else:
        code = os.waitstatus_to_exitcode(status)
    while True:   # whatever it wrote last
        ready, _, _ = select.select([master], [], [], 0.2)
        if master not in ready:
            break
        try:
            data = os.read(master, 65536)
        except OSError:
            break
        if not data:
            break
        out += data
    after = termios.tcgetattr(slave)
    os.close(master)
    os.close(slave)
    return code, bytes(out), before, after


def common(name, code, out, before, after, expect_code=0):
    check(code is not None, name + ": finished (no hang)")
    if expect_code is not None:
        check(code == expect_code, name + ": exit code %r" % code)
    check(b"\x1b[?1049h" in out, name + ": switched to the alternate screen")
    check(out.rfind(b"\x1b[?1049l") > out.rfind(b"\x1b[?1049h"), name + ": left the alternate screen at the end")
    # lflag / iflag: echo, line editing and signals are back exactly as before
    check(after[3] == before[3] and after[0] == before[0], name + ": terminal settings restored")


def main():
    tmp = tempfile.mkdtemp()
    log = os.path.join(tmp, "autotest.log")

    print("1. plain terminal, scripted 6 second run")
    code, out, before, after = run(["--autotest", "6", log, "--nosound"], PLAIN, keys=[(1.0, b"w"), (1.1, b"w"), (2.0, b"\x1b[<35;60;20M")])
    common("autotest", code, out, before, after)
    check("▀".encode() in out or "▄".encode() in out, "autotest: drew half-block pixels")
    check(os.path.exists(log), "autotest: wrote its log")
    if os.path.exists(log):
        print("     " + open(log).read().replace("\n", "\n     "))

    print("2. kitty protocol terminal, keys and mouse, then Ctrl+C")
    code, out, before, after = run(["--level", "1", "--nosound"], KITTY, keys=[
        (1.0, b"\x1b[119u"), (1.5, b"\x1b[119;1:3u"),              # w down / up
        (1.8, b"\x1b[<35;500;300M"), (2.0, b"\x1b[<35;700;300M"),  # pointer look (pixel reports)
        (2.2, b"\x1b[<0;700;300M"), (2.3, b"\x1b[<0;700;300m"),    # fire
        (3.0, b"\x1b[99;5u")])                                     # Ctrl+C
    common("kitty + Ctrl+C", code, out, before, after)
    check(b"\x1b[>11u" in out and b"\x1b[<u" in out, "kitty + Ctrl+C: pushed and popped the kitty keyboard flags")
    check(b"\x1b[?1016h" in out, "kitty + Ctrl+C: switched to pixel mouse reports")

    print("3. plain terminal, keys, then SIGTERM")
    code, out, before, after = run(["--level", "2", "--nosound"], PLAIN, keys=[(1.0, b"w"), (1.03, b"w"), (1.5, b"\x1b")],
                                   kill=(2.5, signal.SIGTERM))
    common("SIGTERM", code, out, before, after, expect_code=None)

    print("4. terminalhell --input, quit with q")
    code, out, before, after = run(["--input"], PLAIN, keys=[(0.5, b"w"), (1.0, b"q")])
    common("--input", code, out, before, after)
    check(b"INPUT CHECK" in out, "--input: showed the input check screen")

    if failures:
        print("%d check(s) failed" % len(failures))
        sys.exit(1)
    print("all checks passed")


main()

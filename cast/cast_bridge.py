"""Experimental classic Chromecast live desktop cast (Windows).

Captures the Windows desktop using FFmpeg and serves H.264 MPEG-TS HLS to
Chromecast's Default Media Receiver. This is a *desktop* capture; maximise
the UxPlay mirror window before casting. Audio can be enabled with an
explicit DirectShow input (e.g. a configured loopback/virtual cable).
"""
import argparse
import functools
import http.server
import json
import os
import pathlib
import shutil
import socket
import subprocess
import sys
import tempfile
import threading
import time

import pychromecast

PORT = 27466


def devices():
    casts, browser = pychromecast.get_chromecasts(timeout=8)
    try:
        print(json.dumps([{"name": c.name, "uuid": str(c.uuid), "model": c.model_name}
                          for c in sorted(casts, key=lambda x: x.name.lower())]), flush=True)
    finally:
        pychromecast.discovery.stop_discovery(browser)


class MediaHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-cache, no-store")
        self.send_header("Access-Control-Allow-Origin", "*")
        super().end_headers()

    def log_message(self, format, *args):
        print("http: " + format % args, flush=True)


def ipv4_for(host):
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        sock.connect((host, 8009))
        return sock.getsockname()[0]


def cast(args):
    ffmpeg = pathlib.Path(args.ffmpeg).resolve()
    if not ffmpeg.is_file():
        raise RuntimeError(f"FFmpeg not found: {ffmpeg}")
    casts, browser = pychromecast.get_chromecasts(timeout=10)
    try:
        target = next((c for c in casts if str(c.uuid) == args.uuid), None)
        if target is None:
            raise RuntimeError("Selected Chromecast not detected. Ensure it can reach this PC.")
        local_ip = ipv4_for(target.host)
        target.wait(timeout=15)
        with tempfile.TemporaryDirectory(prefix="airbridge-cast-") as directory:
            dest = pathlib.Path(directory)
            handler = functools.partial(MediaHandler, directory=str(dest))
            server = http.server.ThreadingHTTPServer(("0.0.0.0", PORT), handler)
            threading.Thread(target=server.serve_forever, daemon=True).start()
            command = [
                str(ffmpeg), "-hide_banner", "-loglevel", "warning", "-y",
                "-f", "gdigrab", "-framerate", "25", "-i", "desktop"
            ]
            if args.audio_device:
                command.extend(["-f", "dshow", "-i", f"audio={args.audio_device}"])
            command.extend([
                "-map", "0:v:0", "-c:v", "libx264", "-preset", "ultrafast",
                "-tune", "zerolatency", "-pix_fmt", "yuv420p",
                "-profile:v", "baseline", "-level", "3.1",
                "-vf", "scale=w=1280:h=720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2",
                "-r", "25", "-g", "50", "-keyint_min", "50", "-sc_threshold", "0",
                "-b:v", "2000k", "-maxrate", "2500k", "-bufsize", "4000k",
            ])
            if args.audio_device:
                command.extend(["-map", "1:a:0", "-c:a", "aac", "-b:a", "128k", "-ar", "44100"])
            else:
                command.extend(["-an"])
            command.extend([
                "-f", "hls", "-hls_time", "2", "-hls_list_size", "6",
                "-hls_flags", "delete_segments+independent_segments+omit_endlist",
                "-hls_segment_filename", str(dest / "chunk%06d.ts"),
                str(dest / "live.m3u8"),
            ])
            print("Starting FFmpeg capture. Maximise the iPhone mirror window.", flush=True)
            process = subprocess.Popen(command, stdin=subprocess.DEVNULL)
            try:
                playlist = dest / "live.m3u8"
                deadline = time.monotonic() + 20
                while (not playlist.exists() or not list(dest.glob("*.ts"))) and time.monotonic() < deadline:
                    if process.poll() is not None:
                        raise RuntimeError(f"FFmpeg ended with code {process.returncode}")
                    time.sleep(.25)
                if not playlist.exists():
                    raise RuntimeError("FFmpeg failed to create HLS stream")
                url = f"http://{local_ip}:{PORT}/live.m3u8"
                print(f"Casting to {target.name}: {url}", flush=True)
                target.media_controller.play_media(url, "application/vnd.apple.mpegurl",
                    stream_type="LIVE", title="AirBridge live screen")
                target.media_controller.block_until_active(timeout=20)
                print("Cast started. Press Stop Cast in AirBridge to end.", flush=True)
                while process.poll() is None:
                    time.sleep(1)
                raise RuntimeError(f"FFmpeg stopped with code {process.returncode}")
            finally:
                target.media_controller.stop()
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=3)
                    except subprocess.TimeoutExpired:
                        process.kill()
                server.shutdown()
    finally:
        pychromecast.discovery.stop_discovery(browser)


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list")
    play = sub.add_parser("cast")
    play.add_argument("--uuid", required=True)
    play.add_argument("--ffmpeg", required=True)
    play.add_argument("--audio-device", default="")
    args = parser.parse_args()
    try:
        if args.cmd == "list":
            devices()
        else:
            cast(args)
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr, flush=True)
        sys.exit(1)


if __name__ == "__main__":
    main()

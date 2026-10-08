"""Local-only OCR for Finger Service under Wine. Python standard library only."""
import csv
import hmac
import io
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
from http.server import BaseHTTPRequestHandler, HTTPServer


def parse_tsv(text):
    lines = {}
    for row in csv.DictReader(io.StringIO(text), delimiter="\t", quoting=csv.QUOTE_NONE):
        if row["level"] != "5" or not row["text"].strip():
            continue
        key = tuple(row[k] for k in ("page_num", "block_num", "par_num", "line_num"))
        x, y, w, h = (int(row[k]) for k in ("left", "top", "width", "height"))
        line = lines.setdefault(key, {"words": [], "left": x, "top": y, "right": x+w, "bottom": y+h})
        line["words"].append(row["text"])
        line["left"] = min(line["left"], x)
        line["top"] = min(line["top"], y)
        line["right"] = max(line["right"], x+w)
        line["bottom"] = max(line["bottom"], y+h)
    return [{"text": " ".join(v["words"]), "x": (v["left"]+v["right"])/2,
             "y": (v["top"]+v["bottom"])/2} for v in lines.values()]


def recognize(image):
    with tempfile.TemporaryDirectory(prefix="cj-ocr-") as folder:
        path = Path(folder) / "window.png"
        path.write_bytes(image)
        result = subprocess.run(["tesseract", str(path), "stdout", "-l", "tha+eng",
                                 "--psm", "11", "tsv"], capture_output=True, timeout=20,
                                encoding="utf-8", check=True,
                                env={**os.environ, "OMP_THREAD_LIMIT": "2"})
        return parse_tsv(result.stdout)


class Handler(BaseHTTPRequestHandler):
    def setup(self):
        super().setup()
        self.connection.settimeout(10)

    def log_message(self, *_):
        pass  # Do not log screenshot contents, tokens or recognized text.

    def reply(self, status, payload):
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def do_POST(self):
        if self.path != "/ocr":
            return self.reply(404, {"error": "Not found"})
        if not hmac.compare_digest(self.headers.get("X-OCR-Token", "").encode(), self.server.token.encode()):
            return self.reply(401, {"error": "Invalid token"})
        try:
            size = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            return self.reply(400, {"error": "Invalid length"})
        if not 0 < size <= 10*1024*1024:
            return self.reply(413, {"error": "Image must be under 10 MB"})
        image = self.rfile.read(size)
        if len(image) != size or not image.startswith(b"\x89PNG\r\n\x1a\n"):
            return self.reply(400, {"error": "PNG required"})
        try:
            self.reply(200, recognize(image))
        except subprocess.TimeoutExpired:
            self.reply(504, {"error": "OCR timed out"})
        except (OSError, subprocess.CalledProcessError, ValueError, KeyError):
            self.reply(503, {"error": "Check Tesseract and tha+eng language data"})


def main():
    languages = subprocess.run(["tesseract", "--list-langs"], capture_output=True,
                               text=True, check=True).stdout.splitlines()
    if not {"tha", "eng"}.issubset(languages):
        raise SystemExit("Install tesseract-ocr-tha and tesseract-ocr-eng first.")
    root = Path.home() / ".config" / "cj-finger-service"
    root.mkdir(parents=True, exist_ok=True, mode=0o700)
    token_file = root / "ocr-token"
    if not token_file.exists():
        with token_file.open("x", encoding="ascii") as file:
            os.chmod(token_file, 0o600)
            file.write(secrets.token_hex(32))
    token = token_file.read_text(encoding="ascii").strip()
    if len(token) < 32:
        raise SystemExit("OCR token must contain at least 32 characters.")
    server = HTTPServer(("127.0.0.1", 17863), Handler)
    server.token = token
    print(f"OCR ready on 127.0.0.1:17863. Token file: {token_file}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()

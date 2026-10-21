"""Preview exact ZIP entries from an APK; never extract through Windows paths.

This checks packaged WebView assets in a browser, not the Android runtime.
The native-contract.html route injects a test bridge from tools/, outside the APK.
"""
import argparse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit
from zipfile import ZipFile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("apk", type=Path)
parser.add_argument("--port", type=int, default=4181)
args = parser.parse_args()
archive = ZipFile(args.apk.resolve())
# Python ZipInfo.filename normalizes backslashes on Windows while reading too.
# Use orig_filename as the lookup key so this preview exposes the Android bug.
exact_entries = {entry.orig_filename: entry for entry in archive.infolist()}
mime_types = {".html": "text/html", ".mjs": "text/javascript", ".js": "text/javascript",
              ".css": "text/css", ".png": "image/png", ".json": "application/json",
              ".tsv": "text/plain", ".txt": "text/plain"}


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        path = unquote(urlsplit(self.path).path)
        if "\\" in path or ".." in path.split("/"):
            self.send_error(403)
            return
        if path == "/__tests/native-contract-mock.js":
            body = Path(__file__).with_name("native-contract-mock.js").read_bytes()
        else:
            if not path.startswith("/assets/"):
                self.send_error(404)
                return
            native_test = path == "/assets/native-contract.html"
            entry = "assets/index.html" if native_test else path[1:]
            try:
                body = archive.read(exact_entries[entry])
            except KeyError:
                self.send_error(404, "Packaged asset missing")
                return
            if native_test:
                body = body.replace(b'<script type="module" src="app.mjs">',
                                    b'<script src="/__tests/native-contract-mock.js"></script>'
                                    b'<script type="module" src="app.mjs">')
        mime = mime_types.get(Path(path).suffix, "application/octet-stream")
        self.send_response(200)
        self.send_header("Content-Type", mime + ("; charset=utf-8" if mime != "image/png" else ""))
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


print(f"APK assets: http://127.0.0.1:{args.port}/assets/index.html", flush=True)
try:
    ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()
finally:
    archive.close()

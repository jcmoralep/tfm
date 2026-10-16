"""Minimal local SMTP sink for walkthroughs: accepts plain SMTP on 127.0.0.1:1025 and appends each message to a file.

Usage (Windows paths for the Windows Python): python -I smtp_sink.py C:/path/to/mails.txt
Pair it with Smtp__EnableSsl=false (the app accepts that only for localhost/127.0.0.1).
Message bodies are base64; decode the part after the blank line to read links.
"""
import socketserver
import sys

OUT = sys.argv[1]


class Handler(socketserver.StreamRequestHandler):
    def handle(self):
        def reply(line):
            self.wfile.write((line + "\r\n").encode())

        reply("220 sink ESMTP")
        in_data, lines = False, []
        while True:
            raw = self.rfile.readline()
            if not raw:
                break
            text = raw.decode("utf-8", "replace").rstrip("\r\n")
            if in_data:
                if text == ".":
                    with open(OUT, "a", encoding="utf-8") as f:
                        f.write("\n=====MSG=====\n" + "\n".join(lines) + "\n")
                    lines, in_data = [], False
                    reply("250 ok")
                else:
                    lines.append(text)
                continue
            upper = text.upper()
            if upper.startswith("EHLO"):
                reply("250-sink")
                reply("250 8BITMIME")
            elif upper.startswith("DATA"):
                in_data = True
                reply("354 go")
            elif upper.startswith("QUIT"):
                reply("221 bye")
                break
            else:
                reply("250 ok")


class Server(socketserver.ThreadingTCPServer):
    # False: on Windows True would let a second sink bind the same port silently.
    allow_reuse_address = False


Server(("127.0.0.1", 1025), Handler).serve_forever()

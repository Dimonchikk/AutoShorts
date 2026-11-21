"""Публікує пост про новий реліз у Telegram-канал з кнопками «Завантажити» і «Сайт»."""
import html
import json
import os
import re
import sys
import urllib.request

token = os.environ.get("TELEGRAM_BOT_TOKEN", "")
chat_id = os.environ.get("TELEGRAM_CHAT_ID", "")
repo = os.environ["REPO"]
tag = os.environ["TAG"]

if not token or not chat_id:
    print("TELEGRAM_BOT_TOKEN / TELEGRAM_CHAT_ID не задані — пропускаю.")
    sys.exit(0)


def gh(url):
    req = urllib.request.Request(url, headers={
        "Authorization": "Bearer " + os.environ.get("GH_TOKEN", ""),
        "Accept": "application/vnd.github+json",
        "User-Agent": "autoshorts-telegram",
    })
    with urllib.request.urlopen(req) as r:
        return json.load(r)


release = gh(f"https://api.github.com/repos/{repo}/releases/tags/{tag}")
body = release.get("body") or ""
version = tag.lstrip("vV")

exes = [a for a in release.get("assets", []) if a["name"].lower().endswith(".exe")]
asset = next((a for a in exes if "setup" in a["name"].lower()), exes[0] if exes else None)
download = asset["browser_download_url"] if asset else release["html_url"]
owner, name = repo.split("/")
site = f"https://{owner.lower()}.github.io/{name}/"

lines = []
for line in body.splitlines():
    line = line.strip()
    if not line:
        continue
    line = re.sub(r"^[-*]\s+", "• ", line)
    lines.append(html.escape(line))

text = f"🎬 <b>AutoShorts {html.escape(version)}</b>\n\n" + "\n".join(lines)
if len(text) > 3900:
    text = text[:3900] + "…"

payload = {
    "chat_id": chat_id,
    "text": text,
    "parse_mode": "HTML",
    "disable_web_page_preview": True,
    "reply_markup": {"inline_keyboard": [[
        {"text": f"⬇️ Завантажити {version}", "url": download},
        {"text": "🌐 Сайт", "url": site},
    ]]},
}
req = urllib.request.Request(
    f"https://api.telegram.org/bot{token}/sendMessage",
    data=json.dumps(payload).encode("utf-8"),
    headers={"Content-Type": "application/json"},
)
with urllib.request.urlopen(req) as r:
    print(r.read().decode("utf-8"))

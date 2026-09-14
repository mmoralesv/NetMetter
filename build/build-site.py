#!/usr/bin/env python3
"""Build the NetMetter GitHub Pages site into _site/.

docs/PRIVACY.md is the single source of truth for the privacy policy; this renders it
to privacy.html and generates a small landing page. Run by .github/workflows/pages.yml.

    pip install markdown
    python build/build-site.py
"""
from __future__ import annotations

import pathlib

import markdown

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "_site"
STORE_ID = "9PHLZGZZN7CB"
STORE_URL = f"https://apps.microsoft.com/detail/{STORE_ID}"

TEMPLATE = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<style>
  :root {{ color-scheme: light dark; --bg:#f4f6f8; --card:#fff; --ink:#15202b; --muted:#56636f; --line:#d9dfe5; --accent:#0b63b5; }}
  @media (prefers-color-scheme: dark) {{ :root {{ --bg:#0e1318; --card:#151c23; --ink:#e3e8ed; --muted:#95a3b0; --line:#28333d; --accent:#62aef0; }} }}
  * {{ box-sizing: border-box; }}
  body {{ margin:0; background:var(--bg); color:var(--ink); font:16px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif; }}
  main {{ max-width:44rem; margin:0 auto; padding:2.5rem 1.25rem 4rem; }}
  .card {{ background:var(--card); border:1px solid var(--line); border-radius:12px; padding:1.75rem 2rem; }}
  h1 {{ font-size:2rem; line-height:1.15; margin:.2rem 0 1rem; letter-spacing:-.02em; }}
  h2 {{ font-size:1.25rem; margin:1.8rem 0 .5rem; }}
  a {{ color:var(--accent); }}
  code {{ background:color-mix(in srgb, var(--muted) 18%, transparent); padding:.1em .35em; border-radius:4px; font-size:.9em; }}
  em {{ color:var(--muted); font-style:normal; font-size:.9rem; }}
  .brand {{ display:flex; align-items:center; gap:.6rem; font-weight:600; margin-bottom:1.5rem; }}
  .tile {{ width:28px; height:28px; border-radius:7px; background:#0078d4; display:grid; place-items:center; color:#fff; font-weight:700; font-size:.9rem; }}
  .btn {{ display:inline-block; margin-top:.5rem; padding:.6rem 1rem; background:var(--accent); color:#fff; border-radius:8px; text-decoration:none; font-weight:600; }}
  .foot {{ margin-top:2rem; color:var(--muted); font-size:.85rem; }}
</style>
</head>
<body>
<main>
  <div class="brand"><span class="tile">↑↓</span> NetMetter</div>
  <div class="card">
{body}
  </div>
  <p class="foot">NetMetter · <a href="./">Home</a> · <a href="./privacy.html">Privacy</a> · <a href="{store}">Microsoft Store</a></p>
</main>
</body>
</html>
"""

INDEX_BODY = f"""<h1>Live network speed on your taskbar</h1>
<p>NetMetter shows the real-time upload and download speed of every connected network
adapter &mdash; Ethernet, Wi-Fi, VPN and more &mdash; as a compact readout on your
taskbar or a small floating window. Free, lightweight, and completely private: it reads
network information locally and sends nothing anywhere.</p>
<p><a class="btn" href="{STORE_URL}">Get it from the Microsoft Store</a></p>
<h2>Links</h2>
<ul>
  <li><a href="./privacy.html">Privacy policy</a></li>
  <li><a href="https://github.com/mmoralesv/NetMetter">Source on GitHub</a></li>
</ul>"""


def render(title: str, body_html: str) -> str:
    return TEMPLATE.format(title=title, body=body_html, store=STORE_URL)


def main() -> None:
    OUT.mkdir(exist_ok=True)

    privacy_md = (ROOT / "docs" / "PRIVACY.md").read_text(encoding="utf-8")
    privacy_html = markdown.markdown(privacy_md, extensions=["extra", "sane_lists"])
    (OUT / "privacy.html").write_text(
        render("NetMetter — Privacy Policy", privacy_html), encoding="utf-8"
    )

    (OUT / "index.html").write_text(
        render("NetMetter — Live network speed on your taskbar", INDEX_BODY),
        encoding="utf-8",
    )

    # Skip Jekyll processing of the artifact.
    (OUT / ".nojekyll").write_text("", encoding="utf-8")
    print(f"Built site into {OUT}")


if __name__ == "__main__":
    main()

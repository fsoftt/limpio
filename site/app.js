(function () {
  "use strict";
  var D = window.I18N, root = document.documentElement;
  var LANGS = ["es", "en", "pt"];
  var reduce = window.matchMedia && matchMedia("(prefers-reduced-motion: reduce)").matches;

  function store(k, v) { try { if (v === undefined) return localStorage.getItem(k); localStorage.setItem(k, v); } catch (e) {} return null; }
  function norm(l) { l = String(l || "").toLowerCase().slice(0, 2); return LANGS.indexOf(l) > -1 ? l : null; }

  function detect() {
    var q = null;
    try { q = norm(new URLSearchParams(location.search).get("lang")); } catch (e) {}
    if (q) { store("lang", q); return q; }
    var s = norm(store("lang"));
    if (s) return s;
    var nl = navigator.languages || [navigator.language];
    for (var i = 0; i < nl.length; i++) { var n = norm(nl[i]); if (n) return n; }
    return "es";
  }

  var lang = "es", view = "after";
  function t(k) { return D[lang][k] !== undefined ? D[lang][k] : D.es[k]; }
  function fmt(n) { return n.toLocaleString(lang === "en" ? "en-US" : lang === "pt" ? "pt-BR" : "es-ES", { maximumFractionDigits: 0, useGrouping: false }); }

  function setMeta(sel, attr, val) { var el = document.querySelector(sel); if (el) el.setAttribute(attr, val); }

  function applyLang(l, persist) {
    lang = l; root.lang = l === "pt" ? "pt-BR" : l;
    if (persist) store("lang", l);
    document.querySelectorAll("[data-i18n]").forEach(function (e) { e.textContent = t(e.getAttribute("data-i18n")); });
    document.querySelectorAll("[data-i18n-alt]").forEach(function (e) { e.setAttribute("alt", t(e.getAttribute("data-i18n-alt"))); });
    document.querySelectorAll("[data-i18n-aria]").forEach(function (e) { e.setAttribute("aria-label", t(e.getAttribute("data-i18n-aria"))); });
    document.title = t("title");
    setMeta('meta[name="description"]', "content", t("desc"));
    setMeta('meta[property="og:title"]', "content", t("title"));
    setMeta('meta[name="twitter:title"]', "content", t("title"));
    setMeta('meta[property="og:description"]', "content", t("desc"));
    setMeta('meta[name="twitter:description"]', "content", t("desc"));
    document.getElementById("lang").value = l;
    var lb = document.getElementById("lb-img");
    if (lb.dataset.altkey) lb.alt = t(lb.dataset.altkey);
    renderDeltas();
  }

  /* ---------- theme ---------- */
  var themeBtn = document.getElementById("theme");
  var st = store("theme");
  if (st === "light" || st === "dark") root.setAttribute("data-theme", st);
  themeBtn.addEventListener("click", function () {
    var cur = root.getAttribute("data-theme") || (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
    var next = cur === "dark" ? "light" : "dark";
    root.setAttribute("data-theme", next); store("theme", next);
  });

  /* ---------- showcase ---------- */
  var cards = Array.prototype.slice.call(document.querySelectorAll(".metric"));
  var cur = cards.map(function (c) { return +c.dataset.after; });
  var raf = null;

  function paint(c, v) {
    c.querySelector(".v").textContent = fmt(Math.round(v));
    var max = +c.dataset.max, pct = Math.max(0, Math.min(100, v / max * 100));
    var arc = c.querySelector(".val");
    if (arc) arc.style.strokeDashoffset = String(100 - pct);
    var bar = c.querySelector(".bar-g i");
    if (bar) bar.style.width = pct + "%";
  }
  function renderDeltas() {
    cards.forEach(function (c) {
      var b = +c.dataset.before, a = +c.dataset.after, d = a - b;
      var unit = c.querySelector(".num small") && c.querySelector(".num small").textContent === "MB" ? " MB" : "";
      var sign = d > 0 ? "+" : "−";
      var ch = sign + fmt(Math.abs(d)) + unit + " (" + sign + Math.round(Math.abs(d) / b * 100) + "%)";
      if (c.classList.contains("ring")) ch = sign + fmt(Math.abs(d)) + " pts";
      var txt = view === "after" ? t("d_text").replace("{b}", fmt(b) + unit).replace("{c}", ch) : t("d_now").replace("{b}", fmt(b) + unit);
      c.querySelector(".delta").textContent = txt;
    });
    document.getElementById("seg-before").setAttribute("aria-pressed", view === "before");
    document.getElementById("seg-after").setAttribute("aria-pressed", view === "after");
  }
  function setView(v, instant) {
    view = v; renderDeltas();
    var from = cur.slice(), to = cards.map(function (c) { return +c.dataset[v]; });
    cancelAnimationFrame(raf);
    if (reduce || instant) { cur = to; cards.forEach(function (c, i) { paint(c, to[i]); }); return; }
    var t0 = null, dur = 900;
    (function step(ts) {
      if (t0 === null) t0 = ts;
      var p = Math.min(1, (ts - t0) / dur), e = 1 - Math.pow(1 - p, 3);
      cards.forEach(function (c, i) { paint(c, from[i] + (to[i] - from[i]) * e); });
      if (p < 1) raf = requestAnimationFrame(step); else cur = to;
    })(performance.now());
  }
  document.getElementById("seg-before").addEventListener("click", function () { setView("before"); });
  document.getElementById("seg-after").addEventListener("click", function () { setView("after"); });

  // Start from "before" and play to "after" when the section scrolls into view.
  var played = false;
  function start() {
    cards.forEach(function (c) { paint(c, +c.dataset.after); });
    if (reduce || !("IntersectionObserver" in window)) return;
    cur = cards.map(function (c) { return +c.dataset.before; });
    cards.forEach(function (c, i) { paint(c, cur[i]); });
    var io = new IntersectionObserver(function (es) {
      if (es[0].isIntersecting && !played) { played = true; setView("after"); io.disconnect(); }
    }, { threshold: 0.35 });
    io.observe(document.getElementById("metrics"));
  }

  /* ---------- lightbox ---------- */
  var lb = document.getElementById("lb"), lbi = document.getElementById("lb-img");
  document.querySelectorAll(".shot").forEach(function (b) {
    b.addEventListener("click", function () {
      lbi.src = b.dataset.src; lbi.dataset.altkey = b.dataset.altkey; lbi.alt = t(b.dataset.altkey);
      if (lb.showModal) lb.showModal(); else lb.setAttribute("open", "");
    });
  });
  function closeLb() { if (lb.close) lb.close(); else lb.removeAttribute("open"); }
  document.getElementById("lb-close").addEventListener("click", closeLb);
  lb.addEventListener("click", function (e) { if (e.target === lb) closeLb(); });

  /* ---------- language ---------- */
  document.getElementById("lang").addEventListener("change", function (e) {
    applyLang(e.target.value, true);
    try { var u = new URL(location.href); u.searchParams.set("lang", e.target.value); history.replaceState(null, "", u); } catch (x) {}
  });

  applyLang(detect(), false);
  start();
})();

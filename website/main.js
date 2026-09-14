/* Peeklism — 產品介紹網站
   三站共用：navHairline()、revealOnScroll()
   Peeklism 專屬：spaceBarPreview()（兄弟站把這一個函式整組換成自己的母題）      */
(function () {
  'use strict';

  var reduced = window.matchMedia('(prefers-reduced-motion: reduce)');

  /* ---------------------------------------------------------------- 共用 */

  /** 捲過 hero 之後才給導覽列下緣分隔線。 */
  function navHairline() {
    var nav = document.getElementById('nav');
    var hero = document.querySelector('.hero');
    if (!nav || !hero || !('IntersectionObserver' in window)) return;

    new IntersectionObserver(function (entries) {
      nav.classList.toggle('is-stuck', !entries[0].isIntersecting);
    }, { rootMargin: '-100% 0px 0px 0px' }).observe(hero);
  }

  /** 捲動揭示。初始狀態在 HTML 裡就是可見的，位移只在 JS 接手後才加上。 */
  function revealOnScroll() {
    if (reduced.matches || !('IntersectionObserver' in window)) return;

    var targets = document.querySelectorAll(
      '.sec-head, .motif-head, .motif, .motif-notes > *, .shot-pair figure,' +
      '.diagram-wrap, .prose-cols > *, .split-copy, .split-shot, .tray-card,' +
      '.table-scroll, .notes-pair > *, .install-notes, .cta-strip, .fam-card'
    );
    if (!targets.length) return;

    document.documentElement.classList.add('js-reveal');
    Array.prototype.forEach.call(targets, function (el) { el.classList.add('reveal'); });

    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-in');
        observer.unobserve(entry.target);
      });
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.08 });

    Array.prototype.forEach.call(targets, function (el) { observer.observe(el); });
  }

  /* ------------------------------------------------- Peeklism 的那一個時刻 */

  /* 示範資料。全部寫死在這一頁裡，不連線、也不讀取任何本機檔案。
     檔名是為了這個示範編出來的，不是誰的個人檔案。 */
  var FILES = [
    {
      name: '海岸線.jpg', icon: 'i-image', kind: 'image',
      sub: 'JPG 檔案　·　2.4 MB　·　2026/09/02 18:41',
      width: 100, open: '用預設的相片程式開啟這個檔案'
    },
    {
      name: '發行說明.md', icon: 'i-markdown', kind: 'markdown',
      sub: 'MD 檔案　·　937 B　·　2026/09/11 09:12',
      width: 76, open: '用預設的文字編輯器開啟這個檔案'
    },
    {
      name: '設定.json', icon: 'i-text', kind: 'text',
      sub: 'JSON 檔案　·　602 B　·　2026/09/11 09:30',
      width: 90, open: '用預設的文字編輯器開啟這個檔案'
    },
    {
      name: '年度報告.pdf', icon: 'i-pdf', kind: 'pdf',
      sub: 'PDF 檔案　·　4.1 MB　·　2026/08/28 14:05',
      width: 64, open: '用預設的 PDF 閱讀器開啟這個檔案'
    },
    {
      name: '示範片段.mp4', icon: 'i-video', kind: 'video',
      sub: 'MP4 檔案　·　18.6 MB　·　2026/09/05 20:17',
      width: 94, open: '用預設的播放程式開啟這個檔案'
    },
    {
      name: '素材', icon: 'i-folder', kind: 'folder',
      sub: '資料夾　·　5 個項目',
      width: 56, open: '開啟這個資料夾'
    },
    {
      name: '封存.7z', icon: 'i-unknown', kind: 'unknown',
      sub: '7Z 檔案　·　311 MB　·　2026/07/19 11:58',
      width: 60, open: '用預設程式開啟這個檔案'
    }
  ];

  var SVG_NS = 'http://www.w3.org/2000/svg';

  function svgIcon(id) {
    var svg = document.createElementNS(SVG_NS, 'svg');
    var use = document.createElementNS(SVG_NS, 'use');
    use.setAttribute('href', '#' + id);
    svg.setAttribute('aria-hidden', 'true');
    svg.appendChild(use);
    return svg;
  }

  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
  }

  /* 各種類型的預覽內容，畫出來的示意，不是真的檔案。 */
  function bodyFor(kind) {
    var host = el('div', 'pv pv-' + kind);

    if (kind === 'image') {
      var art = document.createElementNS(SVG_NS, 'svg');
      art.setAttribute('viewBox', '0 0 320 200');
      art.setAttribute('class', 'pv-art');
      art.setAttribute('aria-hidden', 'true');
      art.innerHTML =
        '<defs><linearGradient id="pvSky" x1="0" y1="0" x2="0" y2="1">' +
        '<stop offset="0" stop-color="#7FD4EE"/><stop offset="1" stop-color="#E9F6F4"/>' +
        '</linearGradient><linearGradient id="pvSea" x1="0" y1="0" x2="0" y2="1">' +
        '<stop offset="0" stop-color="#2E9FC4"/><stop offset="1" stop-color="#14607E"/>' +
        '</linearGradient></defs>' +
        '<rect width="320" height="200" fill="url(#pvSky)"/>' +
        '<circle cx="248" cy="52" r="20" fill="#FFF3D6"/>' +
        '<path d="M0 128 C 60 116 108 138 168 130 C 222 123 268 140 320 132 L320 200 L0 200 Z" fill="url(#pvSea)"/>' +
        '<path d="M0 126 C 54 112 96 100 148 108 C 196 115 248 98 320 110 L320 134 C 254 126 210 140 152 132 C 100 125 58 136 0 144 Z" fill="#F3EBDA"/>' +
        '<path d="M0 96 L46 62 L88 96 Z" fill="#4C7E96" opacity=".55"/>' +
        '<path d="M52 96 L104 54 L156 96 Z" fill="#3C6A82" opacity=".65"/>';
      host.appendChild(art);
      return host;
    }

    if (kind === 'markdown') {
      host.appendChild(el('div', 'pv-h1', '發行說明'));
      host.appendChild(el('div', 'pv-p', '按下空白鍵，預覽就在原地出現；再按一次收回。'));
      host.appendChild(el('div', 'pv-h2', '這一版做了什麼'));
      ['預覽視窗不進入啟動鏈', '方向鍵換檔時只換內容', '影片與音訊預設靜音'].forEach(function (line) {
        var li = el('div', 'pv-li');
        li.appendChild(el('span', 'pv-bullet'));
        li.appendChild(el('span', null, line));
        host.appendChild(li);
      });
      host.appendChild(el('div', 'pv-quote', '預覽是用來決定「是不是這一份」。'));
      return host;
    }

    if (kind === 'text') {
      [
        '{', '  "hotkey": "Space",', '  "dismiss": ["Space", "Escape"],',
        '  "window": {', '    "takesFocus": false,', '    "cornerRadius": 8',
        '  },', '  "pollIntervalMs": 150', '}'
      ].forEach(function (line) { host.appendChild(el('div', 'pv-code', line)); });
      return host;
    }

    if (kind === 'pdf') {
      [1, 2].forEach(function (index) {
        var page = el('div', 'pv-page');
        page.appendChild(el('div', 'pv-page-title'));
        for (var i = 0; i < (index === 1 ? 5 : 3); i++) {
          page.appendChild(el('div', 'pv-line'));
        }
        host.appendChild(page);
      });
      host.appendChild(el('div', 'pv-note', '共 34 頁，預覽顯示前 25 頁。'));
      return host;
    }

    if (kind === 'video') {
      var frame = el('div', 'pv-frame');
      frame.appendChild(el('span', 'pv-play'));
      host.appendChild(frame);
      var bar = el('div', 'pv-transport');
      bar.appendChild(el('span', 'pv-track'));
      bar.appendChild(el('span', 'pv-muted', '靜音'));
      host.appendChild(bar);
      return host;
    }

    if (kind === 'folder') {
      ['紋理', '插圖-01.png', '插圖-02.png', '配色.txt', '字型授權.pdf'].forEach(function (name, index) {
        var row = el('div', 'pv-row');
        var ico = el('span', 'pv-row-ico');
        ico.appendChild(svgIcon(index === 0 ? 'i-folder' : 'i-text'));
        row.appendChild(ico);
        row.appendChild(el('span', null, name));
        host.appendChild(row);
      });
      return host;
    }

    var message = el('div', 'pv-message');
    message.appendChild(svgIcon('i-unknown'));
    message.appendChild(el('p', null, '7Z 檔案沒有可用的預覽'));
    host.appendChild(message);
    return host;
  }

  /**
   * 空白鍵模擬：清單取得焦點後，空白鍵開關預覽、方向鍵換檔案（預覽留在原地只換內容）、
   * Esc 收回。彈出用 Fluent 的縮放曲線，prefers-reduced-motion 時直接出現。
   */
  function spaceBarPreview() {
    var list = document.getElementById('peekList');
    var panel = document.getElementById('peekPanel');
    var empty = document.getElementById('peekEmpty');
    var status = document.getElementById('peekStatus');
    var title = document.getElementById('peekTitle');
    var sub = document.getElementById('peekSub');
    var glyph = document.getElementById('peekGlyph');
    var body = document.getElementById('peekBody');
    var spaceKey = document.getElementById('peekSpace');
    if (!list || !panel || !status || !body || !spaceKey) return;

    var sel = 0;
    var open = false;
    var closeTimer = 0;

    FILES.forEach(function (file, index) {
      var li = el('li', 'peek-file');
      li.id = 'peek-opt-' + index;
      li.setAttribute('role', 'option');
      li.setAttribute('aria-selected', index === sel ? 'true' : 'false');
      var ico = el('span', 'peek-file-ico');
      ico.appendChild(svgIcon(file.icon));
      li.appendChild(ico);
      li.appendChild(el('span', 'peek-file-name', file.name));
      li.addEventListener('click', function () {
        sel = index;
        paint();
        if (open) fill(false);
        list.focus();
      });
      list.appendChild(li);
    });

    function paint() {
      Array.prototype.forEach.call(list.children, function (li, index) {
        li.setAttribute('aria-selected', index === sel ? 'true' : 'false');
      });
      list.setAttribute('aria-activedescendant', 'peek-opt-' + sel);
    }

    /** @param resize 只有「打開」的時候才依內容決定面板寬度，換檔案時不動，跟實際程式一樣。 */
    function fill(resize) {
      var file = FILES[sel];
      title.textContent = file.name;
      sub.textContent = file.sub;
      glyph.setAttribute('href', '#' + file.icon);
      if (resize) panel.style.setProperty('--peek-w', file.width + '%');
      body.textContent = '';
      body.appendChild(bodyFor(file.kind));
    }

    function say(text) { status.textContent = '模擬：' + text; }

    function show() {
      if (open) return;
      window.clearTimeout(closeTimer);
      open = true;
      fill(true);
      empty.hidden = true;
      panel.hidden = false;
      // 讓瀏覽器先認得「隱藏 → 顯示」，縮放曲線才會真的跑。
      void panel.offsetWidth;
      panel.classList.add('is-open');
      say('預覽打開了，顯示「' + FILES[sel].name + '」。方向鍵換檔案，再按一次空白鍵收回。');
    }

    function hide() {
      if (!open) return;
      open = false;
      panel.classList.remove('is-open');
      var finish = function () {
        if (open) return;
        panel.hidden = true;
        empty.hidden = false;
      };
      if (reduced.matches) finish();
      else closeTimer = window.setTimeout(finish, 180);
      say('預覽收回了。實際的 Peeklism 這時候把畫面完全交還給檔案總管。');
    }

    function move(delta) {
      sel = (sel + delta + FILES.length) % FILES.length;
      paint();
      if (open) {
        // 只換內容，面板不改大小、不重新置中，跟實際程式一樣。
        fill(false);
        say('換到「' + FILES[sel].name + '」，預覽留在原地只換內容。');
      } else {
        say('選取「' + FILES[sel].name + '」。按空白鍵預覽。');
      }
    }

    list.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown' || e.key === 'ArrowRight') { e.preventDefault(); move(1); }
      else if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') { e.preventDefault(); move(-1); }
      else if (e.key === ' ' || e.code === 'Space') {
        e.preventDefault();
        if (open) hide(); else show();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        if (open) hide();
        else say('預覽已經是收起來的，Esc 在這時候什麼也不做。');
      } else if (e.key === 'Enter') {
        e.preventDefault();
        say('Enter 會收回預覽，把開啟這件事交還給檔案總管：' + FILES[sel].open + '。這一頁不會真的開啟任何東西。');
      }
    });

    spaceKey.addEventListener('click', function () {
      if (open) hide(); else show();
      list.focus();
    });

    ['keydown', 'keyup'].forEach(function (type) {
      list.addEventListener(type, function (e) {
        if (e.key !== ' ' && e.code !== 'Space') return;
        spaceKey.classList.toggle('is-down', type === 'keydown');
      });
    });

    paint();
    say('清單取得焦點之後，方向鍵換檔案，空白鍵開關預覽。');
  }

  navHairline();
  revealOnScroll();
  spaceBarPreview();
})();

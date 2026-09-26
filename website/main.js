(() => {
  "use strict";

  const files = [
    {
      name: "品牌概念圖.png",
      src: "assets/preview-image.webp",
      alt: "Peeklism 圖片預覽的實際截圖",
      width: 1172,
      height: 867,
    },
    {
      name: "空白鍵預覽說明.md",
      src: "assets/preview-markdown.webp",
      alt: "Peeklism Markdown 預覽的實際截圖",
      width: 1359,
      height: 723,
    },
    {
      name: "預覽設定.json",
      src: "assets/preview-text.webp",
      alt: "Peeklism 純文字預覽的實際截圖",
      width: 859,
      height: 580,
    },
  ];
  const list = document.getElementById("demoFiles");
  const buttons = [...list.querySelectorAll(".demo-file")];
  const panel = document.getElementById("demoPreview");
  const image = document.getElementById("demoImage");
  const empty = document.getElementById("demoEmpty");
  const status = document.getElementById("demoStatus");
  const control = document.getElementById("spaceControl");
  const action = document.getElementById("spaceAction");
  let selected = 0;
  let open = true;

  function render() {
    buttons.forEach((button, index) =>
      button.setAttribute("aria-pressed", String(index === selected)),
    );
    const file = files[selected];
    image.src = file.src;
    image.alt = file.alt;
    image.width = file.width;
    image.height = file.height;
    panel.hidden = !open;
    empty.hidden = open;
    control.setAttribute("aria-expanded", String(open));
    action.textContent = open ? "收回預覽" : "開啟預覽";
    status.textContent = open
      ? `正在預覽${file.name}`
      : `已選取${file.name}。按空白鍵開啟預覽。`;
  }

  function choose(index, focus) {
    selected = (index + files.length) % files.length;
    render();
    if (focus) buttons[selected].focus();
  }

  function toggle() {
    open = !open;
    render();
  }

  buttons.forEach((button, index) =>
    button.addEventListener("click", () => choose(index, false)),
  );
  list.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown" || event.key === "ArrowRight") {
      event.preventDefault();
      choose(selected + 1, true);
    } else if (event.key === "ArrowUp" || event.key === "ArrowLeft") {
      event.preventDefault();
      choose(selected - 1, true);
    } else if (event.key === " " || event.code === "Space") {
      event.preventDefault();
      toggle();
    } else if (event.key === "Escape" && open) {
      event.preventDefault();
      open = false;
      render();
    }
  });
  control.addEventListener("click", toggle);
})();

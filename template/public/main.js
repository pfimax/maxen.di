// Extra icons in the top bar. Most of the look lives in main.css.
export default {
  iconLinks: [
    { icon: "github", href: "https://github.com/pfimax/maxen.di", title: "GitHub" },
    { icon: "box-seam", href: "https://www.nuget.org/packages/Maxen.DI", title: "NuGet" }
  ]
}

// A table cannot scroll sideways by itself and still fill its container, so each one in an article
// gets a wrapper that does the scrolling. Without it, a wide table on a phone would push the page sideways.
function wrapTables() {
  document.querySelectorAll("article table").forEach(table => {
    if (table.parentElement.classList.contains("mx-table-wrap") || table.closest(".mx-compare")) return
    const wrap = document.createElement("div")
    wrap.className = "mx-table-wrap"
    table.parentNode.insertBefore(wrap, table)
    wrap.appendChild(table)
  })
}

if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", wrapTables)
else wrapTables()

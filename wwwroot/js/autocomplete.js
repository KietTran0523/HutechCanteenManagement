document.addEventListener("DOMContentLoaded", () => {
    const wrapper = document.querySelector(".autocomplete-wrapper");
    const form = document.getElementById("productSearchForm");
    const input = document.getElementById("productSearchInput");
    const box = document.getElementById("autocompleteBox");

    if (!wrapper || !form || !input || !box) return;

    const endpoint = wrapper.dataset.autocompleteUrl || "/Home/AutoComplete";
    const isEnglish = wrapper.dataset.culture === "en-US";
    const labels = isEnglish
        ? {
            heading: "Suggested dishes",
            result: "results",
            loading: "Finding dishes...",
            emptyTitle: "No matching dishes",
            emptyHint: "Try another keyword",
            errorTitle: "Suggestions unavailable",
            errorHint: "Press Enter to search instead",
            viewAll: "View all results"
        }
        : {
            heading: "Gợi ý món ăn",
            result: "kết quả",
            loading: "Đang tìm món phù hợp...",
            emptyTitle: "Không tìm thấy món ăn",
            emptyHint: "Thử tìm bằng một từ khóa khác",
            errorTitle: "Chưa thể tải gợi ý",
            errorHint: "Nhấn Enter để tìm kiếm trực tiếp",
            viewAll: "Xem tất cả kết quả"
        };

    let debounceTimer;
    let requestController;
    let activeIndex = -1;

    const open = () => {
        box.hidden = false;
        input.setAttribute("aria-expanded", "true");
    };

    const close = () => {
        box.hidden = true;
        input.setAttribute("aria-expanded", "false");
        input.removeAttribute("aria-activedescendant");
        activeIndex = -1;
    };

    const createState = (icon, title, hint, spinning = false) => {
        box.replaceChildren();
        const state = document.createElement("div");
        state.className = "autocomplete-state";
        state.setAttribute("role", "status");

        const iconWrap = document.createElement("div");
        iconWrap.className = "autocomplete-state-icon";
        const iconElement = document.createElement("i");
        iconElement.className = spinning ? "fa-solid fa-spinner fa-spin" : `fa-solid ${icon}`;
        iconWrap.append(iconElement);

        const strong = document.createElement("strong");
        strong.textContent = title;
        const span = document.createElement("span");
        span.textContent = hint;
        state.append(iconWrap, strong, span);
        box.append(state);
        open();
    };

    const appendHighlightedText = (element, text, term) => {
        const index = text.toLocaleLowerCase().indexOf(term.toLocaleLowerCase());
        if (index < 0) {
            element.textContent = text;
            return;
        }

        element.append(document.createTextNode(text.slice(0, index)));
        const mark = document.createElement("mark");
        mark.textContent = text.slice(index, index + term.length);
        element.append(mark, document.createTextNode(text.slice(index + term.length)));
    };

    const renderResults = (items, term) => {
        box.replaceChildren();

        const header = document.createElement("div");
        header.className = "autocomplete-header";
        const heading = document.createElement("span");
        heading.textContent = labels.heading;
        const count = document.createElement("span");
        count.className = "autocomplete-count";
        count.textContent = `${items.length} ${labels.result}`;
        header.append(heading, count);
        box.append(header);

        items.forEach((item, index) => {
            const name = isEnglish ? (item.nameEn || item.name) : item.name;
            const category = isEnglish ? (item.categoryEn || item.category) : item.category;
            const link = document.createElement("a");
            link.href = `/mon-an/${encodeURIComponent(item.slug)}`;
            link.className = "autocomplete-item";
            link.id = `autocomplete-option-${index}`;
            link.setAttribute("role", "option");
            link.dataset.index = index.toString();

            const image = document.createElement("img");
            image.className = "autocomplete-image";
            image.src = item.image || "/images/default-food.svg";
            image.alt = "";
            image.loading = "lazy";
            image.addEventListener("error", () => { image.src = "/images/default-food.svg"; }, { once: true });

            const content = document.createElement("div");
            content.className = "autocomplete-content";
            const nameElement = document.createElement("div");
            nameElement.className = "autocomplete-name";
            appendHighlightedText(nameElement, name, term);
            const meta = document.createElement("div");
            meta.className = "autocomplete-meta";
            const categoryIcon = document.createElement("i");
            categoryIcon.className = "fa-solid fa-utensils";
            const categoryText = document.createElement("span");
            categoryText.textContent = category || (isEnglish ? "Dish" : "Món ăn");
            meta.append(categoryIcon, categoryText);
            content.append(nameElement, meta);

            const price = document.createElement("span");
            price.className = "autocomplete-price";
            price.textContent = `${Number(item.price).toLocaleString(isEnglish ? "en-US" : "vi-VN")} ₫`;
            const arrow = document.createElement("span");
            arrow.className = "autocomplete-arrow";
            arrow.setAttribute("aria-hidden", "true");
            const arrowIcon = document.createElement("i");
            arrowIcon.className = "fa-solid fa-chevron-right";
            arrow.append(arrowIcon);
            link.append(image, content, price, arrow);
            box.append(link);
        });

        const footer = document.createElement("button");
        footer.type = "submit";
        footer.className = "autocomplete-footer";
        footer.setAttribute("form", form.id);
        footer.textContent = `${labels.viewAll} “${term}”`;
        box.append(footer);
        open();
    };

    const fetchSuggestions = async (term) => {
        requestController?.abort();
        requestController = new AbortController();
        createState("fa-magnifying-glass", labels.loading, "", true);

        try {
            const response = await fetch(`${endpoint}?term=${encodeURIComponent(term)}`, {
                signal: requestController.signal,
                headers: { "Accept": "application/json" }
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const items = await response.json();
            if (input.value.trim() !== term) return;

            if (!Array.isArray(items) || items.length === 0) {
                createState("fa-bowl-food", labels.emptyTitle, labels.emptyHint);
                return;
            }
            renderResults(items, term);
        } catch (error) {
            if (error.name === "AbortError") return;
            console.error("Autocomplete error:", error);
            createState("fa-wifi", labels.errorTitle, labels.errorHint);
        }
    };

    input.addEventListener("input", () => {
        window.clearTimeout(debounceTimer);
        const term = input.value.trim();
        if (term.length < 2) {
            requestController?.abort();
            close();
            return;
        }
        debounceTimer = window.setTimeout(() => fetchSuggestions(term), 250);
    });

    input.addEventListener("keydown", (event) => {
        const options = [...box.querySelectorAll(".autocomplete-item")];
        if (box.hidden || options.length === 0) {
            if (event.key === "Escape") close();
            return;
        }

        if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            options[activeIndex]?.classList.remove("is-active");
            const direction = event.key === "ArrowDown" ? 1 : -1;
            activeIndex = (activeIndex + direction + options.length) % options.length;
            options[activeIndex].classList.add("is-active");
            options[activeIndex].scrollIntoView({ block: "nearest" });
            input.setAttribute("aria-activedescendant", options[activeIndex].id);
        } else if (event.key === "Enter" && activeIndex >= 0) {
            event.preventDefault();
            options[activeIndex].click();
        } else if (event.key === "Escape") {
            event.preventDefault();
            close();
        }
    });

    input.addEventListener("focus", () => {
        if (box.childElementCount > 0 && input.value.trim().length >= 2) open();
    });

    document.addEventListener("pointerdown", (event) => {
        if (!wrapper.contains(event.target)) close();
    });
});

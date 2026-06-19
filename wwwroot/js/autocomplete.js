document.addEventListener("DOMContentLoaded", () => {

    const input = document.getElementById("productSearchInput");
    const box = document.getElementById("autocompleteBox");

    if (!input || !box) return;

    input.addEventListener("input", async () => {

        const term = input.value.trim();

        if (term.length < 1) {
            box.style.display = "none";
            return;
        }

        try {

            const response = await fetch(
                `/Home/AutoComplete?term=${encodeURIComponent(term)}`
            );

            const data = await response.json();

            if (!data || data.length === 0) {

                box.innerHTML = `
                    <div class="autocomplete-item text-center">
                        Không tìm thấy món ăn
                    </div>
                `;

                box.style.display = "block";
                return;
            }

            box.innerHTML = data.map(item => {

                const productName =
                    currentCulture === "en-US"
                        ? (item.nameEn || item.name)
                        : item.name;

                return `
                <a href="/mon-an/${item.slug}"
                   class="autocomplete-item">

                    <div class="autocomplete-row">

                        <img src="${item.image}"
                             class="autocomplete-image"
                             alt="${productName}">

                        <div class="autocomplete-content">

                            <div class="autocomplete-name">
                                ${productName}
                            </div>

                            <div class="autocomplete-meta">
                                ${item.category}
                                ·
                                ${Number(item.price).toLocaleString()} VNĐ
                            </div>

                        </div>

                    </div>

                </a>
                `;
            }).join("");

            box.style.display = "block";

        }
        catch (err) {

            console.error("Autocomplete error:", err);

            box.style.display = "none";
        }
    });

    document.addEventListener("click", function (e) {

        if (
            !input.contains(e.target) &&
            !box.contains(e.target)
        ) {
            box.style.display = "none";
        }
    });
});
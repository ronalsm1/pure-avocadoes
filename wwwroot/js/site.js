(() => {
    const setupDropdown = () => {
        const trigger = document.getElementById("user-menu-button");
        const menu = document.getElementById("user-menu");
        if (!trigger || !menu) return;

        const close = (restoreFocus = false) => {
            menu.hidden = true;
            trigger.setAttribute("aria-expanded", "false");
            if (restoreFocus) {
                trigger.focus();
            }
        };

        trigger.addEventListener("click", (event) => {
            event.stopPropagation();
            const isOpen = trigger.getAttribute("aria-expanded") === "true";
            menu.hidden = isOpen;
            trigger.setAttribute("aria-expanded", String(!isOpen));
        });

        document.addEventListener("click", (event) => {
            if (!menu.hidden && !menu.contains(event.target) && !trigger.contains(event.target)) {
                close(false);
            }
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && !menu.hidden) {
                close(true);
            }
        });
    };

    const setupMobileDrawer = () => {
        const toggle = document.getElementById("mobile-nav-toggle");
        const drawer = document.getElementById("mobile-nav-drawer");
        const backdrop = document.getElementById("mobile-nav-backdrop");
        const closeBtn = document.getElementById("mobile-nav-close");
        if (!toggle || !drawer || !backdrop || !closeBtn) return;

        const getFocusable = () =>
            Array.from(
                drawer.querySelectorAll(
                    "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), summary, [tabindex]:not([tabindex='-1'])"
                )
            ).filter((el) => el.offsetParent !== null);

        const setOpen = (open, restoreFocus = false) => {
            drawer.hidden = !open;
            backdrop.hidden = !open;
            toggle.setAttribute("aria-expanded", String(open));
            document.body.style.overflow = open ? "hidden" : "";
            if (open) {
                closeBtn.focus();
            } else if (restoreFocus) {
                toggle.focus();
            }
        };

        toggle.addEventListener("click", () => setOpen(true, false));
        closeBtn.addEventListener("click", () => setOpen(false, true));
        backdrop.addEventListener("click", () => setOpen(false, true));

        drawer.querySelectorAll("a[href]").forEach((link) => {
            link.addEventListener("click", () => {
                document.body.style.overflow = "";
            });
        });

        document.addEventListener("keydown", (event) => {
            if (drawer.hidden) return;

            if (event.key === "Escape") {
                setOpen(false, true);
                return;
            }

            if (event.key === "Tab") {
                const focusable = getFocusable();
                if (focusable.length === 0) return;

                const first = focusable[0];
                const last = focusable[focusable.length - 1];

                if (event.shiftKey && (document.activeElement === first || !drawer.contains(document.activeElement))) {
                    event.preventDefault();
                    last.focus();
                } else if (!event.shiftKey && (document.activeElement === last || !drawer.contains(document.activeElement))) {
                    event.preventDefault();
                    first.focus();
                }
            }
        });

        window.addEventListener("resize", () => {
            if (window.innerWidth >= 900 && !drawer.hidden) {
                setOpen(false, false);
            }
        });
    };

    const setupDismissibleAlerts = () => {
        document.querySelectorAll("[data-dismiss-alert]").forEach((btn) => {
            btn.addEventListener("click", () => {
                const alert = btn.closest("[role='status'], [role='alert']");
                if (alert) {
                    alert.remove();
                }
            });
        });
    };

    const setupPosterFallbacks = () => {
        document.querySelectorAll("img[data-poster-img]").forEach((img) => {
            const showFallback = () => {
                img.hidden = true;
                const fallback = img.parentElement?.querySelector("[data-poster-fallback]");
                if (fallback) {
                    fallback.hidden = false;
                }
            };

            img.addEventListener("error", showFallback, { once: true });
            if (img.complete && img.naturalWidth === 0) {
                showFallback();
            }
        });
    };

    const setupStarRatings = () => {
        document.querySelectorAll("[data-star-rating]").forEach((fieldset) => {
            const readout = fieldset.querySelector("[data-rating-readout]");
            const radios = Array.from(fieldset.querySelectorAll("input[type='radio'][name='Rating']"));
            const options = Array.from(fieldset.querySelectorAll("[data-star-value]"));

            const paintStars = (activeValue) => {
                options.forEach((opt) => {
                    const value = Number.parseInt(opt.getAttribute("data-star-value") || "0", 10);
                    opt.classList.toggle("is-lit", activeValue > 0 && value <= activeValue);
                });
            };

            const syncFromChecked = () => {
                const checked = radios.find((radio) => radio.checked);
                const selectedValue = checked ? Number.parseInt(checked.value, 10) : 0;
                fieldset.setAttribute("data-selected-rating", String(selectedValue));
                paintStars(selectedValue);
                if (readout) {
                    readout.textContent = selectedValue > 0 ? `${selectedValue} / 5` : "Select 1 to 5 stars";
                }
            };

            radios.forEach((radio) => {
                radio.addEventListener("change", syncFromChecked);
            });

            options.forEach((opt) => {
                const value = Number.parseInt(opt.getAttribute("data-star-value") || "0", 10);
                opt.addEventListener("mouseenter", () => paintStars(value));
                opt.addEventListener("mouseleave", syncFromChecked);
            });

            syncFromChecked();
        });
    };

    const setupCharCounters = () => {
        document.querySelectorAll("[data-char-counter-target]").forEach((field) => {
            const targetId = field.getAttribute("data-char-counter-target");
            if (!targetId) {
                return;
            }

            const counter = document.getElementById(targetId);
            if (!counter) {
                return;
            }

            const maxLength = Number.parseInt(field.getAttribute("maxlength") || "2000", 10);
            const updateCount = () => {
                const count = field.value.length;
                counter.textContent = `${count} / 2,000`;
                counter.classList.toggle("is-near-limit", count >= maxLength - 100);
            };

            field.addEventListener("input", updateCount);
            updateCount();
        });
    };

    const setupCarousels = () => {
        const reducedMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        document.querySelectorAll("[data-carousel]").forEach((carousel) => {
            const track = carousel.querySelector("[data-carousel-track]");
            const prevBtn = carousel.querySelector("[data-carousel-prev]");
            const nextBtn = carousel.querySelector("[data-carousel-next]");
            if (!track || !prevBtn || !nextBtn) {
                return;
            }

            const syncButtons = () => {
                const maxScroll = Math.max(0, track.scrollWidth - track.clientWidth);
                const canScrollLeft = track.scrollLeft > 4;
                const canScrollRight = track.scrollLeft < maxScroll - 4;
                prevBtn.disabled = !canScrollLeft;
                nextBtn.disabled = !canScrollRight;
                const hasOverflow = maxScroll > 8;
                prevBtn.hidden = !hasOverflow;
                nextBtn.hidden = !hasOverflow;
            };

            const scrollStep = (direction) => {
                const step = Math.max(220, Math.round(track.clientWidth * 0.8));
                track.scrollBy({
                    left: direction * step,
                    behavior: reducedMotion ? "auto" : "smooth"
                });
            };

            prevBtn.addEventListener("click", () => scrollStep(-1));
            nextBtn.addEventListener("click", () => scrollStep(1));
            track.addEventListener("scroll", syncButtons, { passive: true });
            window.addEventListener("resize", syncButtons);

            syncButtons();
        });
    };

    // Hero title -> top bar morph.
    //
    // The heading is a real element measured in the hero, and the bar carries an
    // invisible slot at the size and position the condensed title should end up
    // in. Each scroll frame we interpolate between those two rectangles and write
    // a transform, so one continuous title shrinks and travels into the header
    // instead of two separate titles swapping. Progress is also published as
    // --morph-progress so CSS can fade the bar surface and chrome in step.
    const setupStickyDetailsBar = () => {
        const stickyBar = document.querySelector("[data-sticky-movie-bar]");
        const hero = document.querySelector("[data-details-hero]");
        const ghost = document.querySelector("[data-morph-title]");
        const target = document.querySelector("[data-morph-target]");
        const anchor = document.querySelector("[data-hero-title-anchor]");
        const panel = document.querySelector("[data-details-panel]");
        if (!stickyBar || !hero || !ghost || !target || !anchor || !panel) {
            return;
        }

        const root = document.documentElement;
        const reducedMotion = window.matchMedia
            ? window.matchMedia("(prefers-reduced-motion: reduce)").matches
            : false;

        // Landing geometry, captured in viewport coordinates. The resting spot is
        // read live each frame because the hero is sticky.
        let startHeight = 1;
        let endX = 0;
        let endTop = 0;
        let endHeight = 1;
        let endScale = 0.2;

        // Where the covering card starts, and how far it travels before it reaches
        // the top of the screen. Both are measured, never assumed.
        let span = 1;
        let panelStart = 0;

        const measure = () => {
            // Reset any in-flight transform before measuring the resting layout.
            ghost.style.transform = "none";

            const anchorRect = anchor.getBoundingClientRect();
            const anchorStyle = window.getComputedStyle(anchor);
            const baseFontSize = Number.parseFloat(anchorStyle.fontSize) || 48;

            // The title is drawn on one line so it lands in the bar as a single row.
            // If it is wider than the hero's column, drop the font size until it
            // fits, otherwise long titles run off the edge on small screens.
            ghost.style.fontSize = `${baseFontSize}px`;
            const naturalWidth = ghost.getBoundingClientRect().width;
            const available = Math.max(1, anchorRect.width);
            const fit = naturalWidth > available ? available / naturalWidth : 1;
            const startFontSize = baseFontSize * fit;

            ghost.style.fontSize = `${startFontSize}px`;

            const ghostRect = ghost.getBoundingClientRect();
            startHeight = Math.max(1, ghostRect.height);

            // Size the shrink from the two font sizes, which is the real intent:
            // the hero title ends up at the bar title's size.
            const targetRect = target.getBoundingClientRect();
            const endFontSize = Number.parseFloat(window.getComputedStyle(target).fontSize) || 15;
            const fontScale = Math.min(1, endFontSize / startFontSize);

            // Also cap the landing size by the slot's width, so an unusually long
            // title cannot overflow the bar once it has finished shrinking.
            const slotWidth = Math.max(1, targetRect.width);
            const widthScale = slotWidth / Math.max(1, ghostRect.width);
            endScale = Math.min(fontScale, widthScale);

            // The slot lives inside the fixed bar, so its rect is already in
            // viewport coordinates and does not travel with the page.
            endX = targetRect.left;
            endTop = targetRect.top;
            endHeight = Math.max(1, targetRect.height);

            // The covering card's resting top edge, and how far it has to travel to
            // reach the top of the screen. This is the exact span of the animation.
            panelStart = panel.getBoundingClientRect().top;
            span = Math.max(1, panelStart);
        };

        const apply = () => {
            // Progress is driven by the covering card itself: it is 0 while that
            // card sits at the bottom of the hero and exactly 1 when its top edge
            // reaches the top of the screen. Measuring the real element keeps the
            // handoff perfect regardless of hero height or viewport.
            const panelTop = panel.getBoundingClientRect().top;
            const progress = Math.min(1, Math.max(0, (panelStart - panelTop) / span));

            const scale = 1 + (endScale - 1) * progress;

            // Read the resting spot live rather than caching it: the hero is sticky,
            // so the anchor holds a fixed place on screen once it pins, and the ghost
            // needs to stay welded to it while the covering card slides up.
            const resting = anchor.getBoundingClientRect();
            const x = resting.left + (endX - resting.left) * progress;
            const landedY = endTop + (endHeight - startHeight * scale) / 2;
            const y = resting.top + (landedY - resting.top) * progress;

            ghost.style.transform = `translate3d(${x}px, ${y}px, 0) scale(${scale})`;
            root.style.setProperty("--morph-progress", progress.toFixed(4));

            // The bar itself only becomes visible once its surface is due to paint,
            // so it is not announced or focusable while it is still transparent.
            const visible = progress > 0.8;
            if (stickyBar.getAttribute("data-visible") !== String(visible)) {
                stickyBar.setAttribute("data-visible", visible ? "true" : "false");
                stickyBar.setAttribute("aria-hidden", visible ? "false" : "true");
            }
        };

        let ticking = false;
        const onScroll = () => {
            if (ticking) return;
            ticking = true;
            window.requestAnimationFrame(() => {
                ticking = false;
                apply();
            });
        };

        if (reducedMotion) {
            // No morph: reveal the real heading, hide the floating copy, and only
            // reveal the bar once the hero has scrolled away.
            root.setAttribute("data-morph", "reduced");
            ghost.style.display = "none";
            const observer = new IntersectionObserver(
                (entries) => {
                    entries.forEach((entry) => {
                        const past = !entry.isIntersecting && entry.boundingClientRect.bottom <= 0;
                        stickyBar.setAttribute("data-visible", past ? "true" : "false");
                        stickyBar.setAttribute("aria-hidden", past ? "false" : "true");
                        root.style.setProperty("--morph-progress", past ? "1" : "0");
                    });
                },
                { threshold: 0 }
            );
            observer.observe(hero);
            return;
        }

        root.setAttribute("data-morph", "active");

        measure();
        window.addEventListener("scroll", onScroll, { passive: true });
        window.addEventListener("resize", () => {
            measure();
            apply();
        });
        apply();
    };

    // Search field clear button. The native WebKit cancel button is hidden in
    // CSS (it renders blue), so this mirrors its behaviour with brand styling.
    const setupSearchClear = () => {
        document.querySelectorAll("[data-search-input]").forEach((input) => {
            const form = input.closest("form");
            const clear = form?.querySelector("[data-search-clear]");
            if (!clear) return;

            const sync = () => {
                clear.hidden = input.value.length === 0;
            };

            clear.addEventListener("click", () => {
                input.value = "";
                sync();
                input.focus();
            });

            input.addEventListener("input", sync);
            sync();
        });
    };

    const setupHeroCarousel = () => {
        const root = document.querySelector("[data-hero-carousel]");
        if (!root) return;

        const slides = Array.from(root.querySelectorAll("[data-hero-slide]"));
        const dots = Array.from(root.querySelectorAll("[data-hero-dot]"));
        const prevBtn = root.querySelector("[data-hero-prev]");
        const nextBtn = root.querySelector("[data-hero-next]");
        if (slides.length === 0) return;

        const reducedMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        let current = Math.max(0, slides.findIndex((slide) => slide.classList.contains("is-active")));
        let autoTimer = null;

        const setActive = (index) => {
            current = (index + slides.length) % slides.length;
            root.style.setProperty("--pa-hero-index", String(current));
            slides.forEach((slide, i) => {
                slide.classList.toggle("is-active", i === current);
                slide.setAttribute("aria-hidden", i === current ? "false" : "true");
            });
            dots.forEach((dot, i) => {
                dot.classList.toggle("is-active", i === current);
                dot.setAttribute("aria-current", i === current ? "true" : "false");
            });
        };

        const stopAuto = () => {
            if (autoTimer) {
                window.clearInterval(autoTimer);
                autoTimer = null;
            }
        };

        const startAuto = () => {
            stopAuto();
            if (reducedMotion || slides.length < 2) return;
            autoTimer = window.setInterval(() => setActive(current + 1), 7000);
        };

        prevBtn?.addEventListener("click", () => {
            setActive(current - 1);
            startAuto();
        });
        nextBtn?.addEventListener("click", () => {
            setActive(current + 1);
            startAuto();
        });
        dots.forEach((dot, i) => {
            dot.addEventListener("click", () => {
                setActive(i);
                startAuto();
            });
        });

        root.addEventListener("mouseenter", stopAuto);
        root.addEventListener("mouseleave", startAuto);
        root.addEventListener("focusin", stopAuto);
        root.addEventListener("focusout", startAuto);

        document.addEventListener("keydown", (event) => {
            if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
            if (!root.contains(document.activeElement)) return;
            setActive(event.key === "ArrowLeft" ? current - 1 : current + 1);
        });

        setActive(current);
        startAuto();
    };

    const setupAutoSubmitSelects = () => {
        document.querySelectorAll("[data-auto-submit]").forEach((select) => {
            select.addEventListener("change", () => {
                const form = select.closest("form");
                if (form) {
                    form.submit();
                }
            });
        });
    };

    const setupTabPillSync = () => {
        const rail = document.querySelector("[data-tab-rail]");
        if (!rail) return;
        const pills = Array.from(rail.querySelectorAll("[data-tab-pill]"));
        const targets = pills
            .map((pill) => document.getElementById((pill.getAttribute("href") || "").replace("#", "")))
            .filter(Boolean);
        if (targets.length === 0 || typeof IntersectionObserver !== "function") return;

        const observer = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (!entry.isIntersecting) return;
                    pills.forEach((pill) => {
                        const hash = pill.getAttribute("href") || "";
                        pill.classList.toggle("is-active", hash === `#${entry.target.id}`);
                    });
                });
            },
            { rootMargin: "-20% 0px -65% 0px" }
        );

        targets.forEach((target) => observer.observe(target));
    };

    const setupPasswordToggles = () => {
        const toggles = document.querySelectorAll("[data-password-toggle]");
        toggles.forEach((btn) => {
            const container = btn.parentElement;
            if (!container) {
                return;
            }
            const input = container.querySelector("[data-password-input]");
            if (!input) {
                return;
            }
            const showIcon = btn.querySelector(".pa-eye-icon-show");
            const hideIcon = btn.querySelector(".pa-eye-icon-hide");

            btn.addEventListener("click", () => {
                const isPassword = input.type === "password";
                input.type = isPassword ? "text" : "password";
                btn.setAttribute("aria-pressed", isPassword ? "true" : "false");
                btn.setAttribute("aria-label", isPassword ? "Hide password" : "Show password");
                if (showIcon && hideIcon) {
                    showIcon.hidden = isPassword;
                    hideIcon.hidden = !isPassword;
                }
            });
        });
    };

    setupDropdown();
    setupMobileDrawer();
    setupDismissibleAlerts();
    setupPosterFallbacks();
    setupStarRatings();
    setupCharCounters();
    setupCarousels();
    setupStickyDetailsBar();
    setupPasswordToggles();
    setupHeroCarousel();
    setupAutoSubmitSelects();
    setupTabPillSync();
    setupSearchClear();
})();

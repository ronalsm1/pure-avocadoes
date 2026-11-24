/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        "./Views/**/*.cshtml",
        "./Styles/**/*.css"
    ],
    theme: {
        extend: {
            colors: {
                avocado: {
                    900: "#14261B",
                    800: "#1F3D2A",
                    700: "#2A4A33",
                    600: "#3B4A3F",
                    500: "#546D2E",
                    400: "#718D43",
                    200: "#B8C98A",
                    100: "#EEF2E3"
                },
                gold: {
                    100: "#FBF3DC",
                    500: "#D6AC55",
                    600: "#B5841F"
                },
                paper: {
                    DEFAULT: "#FBFBF7",
                    alt: "#F5F6EF",
                    muted: "#F1F1EC"
                },
                ink: {
                    DEFAULT: "#20251F",
                    muted: "#667064",
                    faint: "#8B948A"
                },
                line: {
                    DEFAULT: "#E2E5DA",
                    strong: "#C9CFC0"
                },
                cinema: {
                    DEFAULT: "#10160F",
                    soft: "#1A231B"
                },
                footer: "#1B2921",
                danger: {
                    fg: "#8C2F26",
                    bg: "#FBEAE7",
                    line: "#EFC3BC"
                },
                success: {
                    fg: "#2F5D34",
                    bg: "#E7F2E4",
                    line: "#BBD8B4"
                },
                warning: {
                    fg: "#7A5A12",
                    bg: "#FBF3DC",
                    line: "#E7D49A"
                }
            },
            fontFamily: {
                sans: [
                    "Franklin Gothic Medium",
                    "Franklin Gothic",
                    "PT Sans",
                    "-apple-system",
                    "BlinkMacSystemFont",
                    "Segoe UI",
                    "Arial",
                    "sans-serif"
                ],
                display: [
                    "Neusa",
                    "Franklin Gothic Medium",
                    "Franklin Gothic",
                    "Impact",
                    "Arial Narrow",
                    "Helvetica",
                    "Arial",
                    "sans-serif"
                ]
            },
            /* One notch up across the whole scale. Sizes are raised without
               touching spacing utilities, so type grows a touch while the
               verified responsive layout stays intact. */
            fontSize: {
                xs: ["0.8125rem", { lineHeight: "1.25rem" }],
                sm: ["0.9375rem", { lineHeight: "1.4rem" }],
                base: ["1.0625rem", { lineHeight: "1.6rem" }],
                lg: ["1.1875rem", { lineHeight: "1.75rem" }],
                xl: ["1.3125rem", { lineHeight: "1.9rem" }],
                "2xl": ["1.5625rem", { lineHeight: "2rem" }],
                "3xl": ["1.9375rem", { lineHeight: "2.25rem" }],
                "4xl": ["2.3125rem", { lineHeight: "2.5rem" }],
                "5xl": ["3rem", { lineHeight: "1" }]
            },
            borderRadius: {
                card: "6px",
                control: "4px"
            },
            transitionTimingFunction: {
                brand: "cubic-bezier(0.4, 0, 0.2, 1)"
            }
        }
    },
    plugins: []
};

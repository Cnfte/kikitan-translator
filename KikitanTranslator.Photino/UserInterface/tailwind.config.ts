import { Config } from "tailwindcss";

export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Geist"', '"SF Pro Display"', '-apple-system', 'BlinkMacSystemFont', '"Helvetica Neue"', '"PingFang SC"', '"Microsoft YaHei"', 'sans-serif'],
        serif: ['"Newsreader"', '"Instrument Serif"', '"Playfair Display"', 'Georgia', 'serif'],
        mono: ['"Geist Mono"', '"SF Mono"', '"JetBrains Mono"', 'Consolas', 'monospace'],
      },
      colors: {
        canvas: "#0a0a0b",
        surface: "#121215",
        "surface-card": "#17171a",
        "surface-raised": "#1f1f23",
        "border-dim": "#26262a",
        "border-subtle": "#323238",
        "border-strong": "#44444c",
        // Deliberate washed-out spot pastels
        pastel: {
          green: {
            bg: "#122316",
            text: "#4ade80",
            border: "#1f3a25",
          },
          blue: {
            bg: "#121f2d",
            text: "#60a5fa",
            border: "#1b354e",
          },
          red: {
            bg: "#271617",
            text: "#f87171",
            border: "#441f23",
          },
          yellow: {
            bg: "#251f12",
            text: "#facc15",
            border: "#40351a",
          },
        },
      },
      borderRadius: {
        DEFAULT: "6px",
        sm: "4px",
        md: "6px",
        lg: "8px",
        xl: "10px",
        "2xl": "12px",
      },
      boxShadow: {
        none: "none",
        subtle: "0 1px 2px rgba(0,0,0,0.2)",
      },
    },
  },
  plugins: [],
} satisfies Config;

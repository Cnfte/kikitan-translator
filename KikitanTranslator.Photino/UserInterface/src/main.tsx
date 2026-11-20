import ReactDOM from "react-dom/client";
import { ThemeProvider, createTheme } from "@mui/material";
import App from "./page";

import "./globals.css";
import { init } from "./util/photino.ts";

const minimalistDarkTheme = createTheme({
  palette: {
    mode: "dark",
    primary: {
      main: "#ffffff",
      contrastText: "#0a0a0b",
    },
    secondary: {
      main: "#8e8e93",
      contrastText: "#ffffff",
    },
    background: {
      default: "transparent",
      paper: "#141417",
    },
    text: {
      primary: "#ededed",
      secondary: "#8e8e93",
    },
    divider: "#26262a",
    success: {
      main: "#4ade80",
      contrastText: "#0a0a0b",
    },
    info: {
      main: "#60a5fa",
      contrastText: "#0a0a0b",
    },
    warning: {
      main: "#facc15",
      contrastText: "#0a0a0b",
    },
    error: {
      main: "#f87171",
      contrastText: "#0a0a0b",
    },
  },
  typography: {
    fontFamily: '"Geist", "SF Pro Display", -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif',
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: "none",
          fontWeight: 600,
          borderRadius: 6,
          boxShadow: "none",
        },
        containedPrimary: {
          backgroundColor: "#ffffff",
          color: "#0a0a0b",
          boxShadow: "none",
          "&:hover": {
            backgroundColor: "#e4e4e7",
            boxShadow: "none",
          },
        },
        containedSecondary: {
          backgroundColor: "#1c1c20",
          color: "#ededed",
          border: "1px solid #2e2e34",
          boxShadow: "none",
          "&:hover": {
            backgroundColor: "#27272c",
            boxShadow: "none",
          },
        },
        outlined: {
          borderColor: "#2e2e34",
          color: "#ededed",
          "&:hover": {
            borderColor: "#ffffff",
            backgroundColor: "rgba(255, 255, 255, 0.04)",
          },
        },
      },
    },
    MuiCheckbox: {
      styleOverrides: {
        root: {
          color: "#52525b",
          "&.Mui-checked": {
            color: "#ffffff",
          },
        },
      },
    },
    MuiRadio: {
      styleOverrides: {
        root: {
          color: "#52525b",
          "&.Mui-checked": {
            color: "#ffffff",
          },
        },
      },
    },
    MuiSwitch: {
      styleOverrides: {
        switchBase: {
          color: "#71717a",
          "&.Mui-checked": {
            color: "#ffffff",
            "& + .MuiSwitch-track": {
              backgroundColor: "#ffffff",
              opacity: 0.5,
            },
          },
        },
        track: {
          backgroundColor: "#2e2e34",
          opacity: 1,
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 6,
          backgroundColor: "#141417",
          color: "#ededed",
          "& .MuiOutlinedInput-notchedOutline": {
            borderColor: "#26262a",
          },
          "&:hover .MuiOutlinedInput-notchedOutline": {
            borderColor: "#44444c",
          },
          "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
            borderColor: "#ffffff",
            borderWidth: 1,
          },
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          borderRadius: 4,
          fontWeight: 600,
          backgroundColor: "#17171a",
          color: "#d4d4d8",
          border: "1px solid #26262a",
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundColor: "#141417",
          backgroundImage: "none",
          border: "1px solid #26262a",
          boxShadow: "none",
          borderRadius: 8,
        },
      },
    },
  },
});

init();

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <ThemeProvider theme={minimalistDarkTheme}>
    <App />
  </ThemeProvider>
);

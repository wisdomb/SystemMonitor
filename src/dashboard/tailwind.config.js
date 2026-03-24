/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{vue,ts,tsx}'],
  theme: {
    extend: {
      // ── Colours — all map to CSS variables so App.vue remains the source of truth ──
      colors: {
        base: 'var(--bg-base)',
        panel: 'var(--bg-panel)',
        surface: 'var(--bg-surface)',
        hover: 'var(--bg-hover)',
        border: 'var(--border)',
        accent: 'var(--accent)',
        'accent-dim': 'var(--accent-dim)',
        success: 'var(--success)',
        warning: 'var(--warning)',
        danger: 'var(--danger)',
        primary: 'var(--text-primary)',
        muted: 'var(--text-muted)',
        dim: 'var(--text-dim)',
      },

      // ── Typography ────────────────────────────────────────────────────────
      fontFamily: {
        mono: ['Space Mono', 'monospace'],
        sans: ['DM Sans', 'sans-serif'],
      },

      // ── Border radius ─────────────────────────────────────────────────────
      borderRadius: {
        DEFAULT: 'var(--radius)',
        sm: '4px',
        lg: '12px',
        xl: '16px',
      },

      // ── Transitions ───────────────────────────────────────────────────────
      transitionDuration: {
        DEFAULT: '180ms',
      },

      // ── Animations ────────────────────────────────────────────────────────
      keyframes: {
        // Skeleton loading shimmer
        shimmer: {
          '0%': { backgroundPosition: '200% 0' },
          '100%': { backgroundPosition: '-200% 0' },
        },
        // Gentle pulse for live indicators
        pulse: {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.3' },
        },
        // Spin for loading spinners
        spin: {
          '0%': { transform: 'rotate(0deg)' },
          '100%': { transform: 'rotate(360deg)' },
        },
        // Slide in from top for toasts/alerts
        slideDown: {
          '0%': { transform: 'translateY(-12px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
        // Slide in from bottom for panels
        slideUp: {
          '0%': { transform: 'translateY(12px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
        // Fade in
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        // Scale pop for badges/counts
        scalePop: {
          '0%': { transform: 'scale(0.85)', opacity: '0' },
          '60%': { transform: 'scale(1.05)' },
          '100%': { transform: 'scale(1)', opacity: '1' },
        },
        // Blink for critical alerts
        blink: {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0' },
        },
        // Data tick — subtle flash when a number updates
        tick: {
          '0%': { color: 'var(--accent)' },
          '100%': { color: 'inherit' },
        },
        // Scan line for terminal effect
        scan: {
          '0%': { backgroundPosition: '0 0' },
          '100%': { backgroundPosition: '0 100%' },
        },
        // Progress bar fill
        fillBar: {
          '0%': { width: '0%' },
          '100%': { width: '100%' },
        },
      },

      animation: {
        shimmer: 'shimmer 1.5s ease-in-out infinite',
        pulse: 'pulse 2s ease-in-out infinite',
        'pulse-fast': 'pulse 1s ease-in-out infinite',
        spin: 'spin 0.8s linear infinite',
        slideDown: 'slideDown 200ms ease forwards',
        slideUp: 'slideUp 220ms ease forwards',
        fadeIn: 'fadeIn 250ms ease forwards',
        scalePop: 'scalePop 300ms cubic-bezier(0.34, 1.56, 0.64, 1) forwards',
        blink: 'blink 1.2s step-end infinite',
        tick: 'tick 600ms ease-out forwards',
        fillBar: 'fillBar 1s ease-out forwards',
      },
    },
  },
  plugins: [],
}
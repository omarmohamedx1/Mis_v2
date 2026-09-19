import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        mis: {
          navy: '#10255C',
          ink: '#0A1F33',
          deep: '#074D73',
          primary: '#0B638F',
          blue: '#1788B8',
          sky: '#7DB8DB',
          pale: '#E7F3FA',
          surface: '#F7F9FC',
          border: '#D8E3EC',
        },
      },
      borderRadius: {
        form: '0.875rem',
      },
      boxShadow: {
        panel: '0 18px 45px rgba(16, 37, 92, 0.11)',
        input: '0 0 0 4px rgba(23, 136, 184, 0.14)',
      },
      fontFamily: {
        sans: ['"IBM Plex Sans"', '"IBM Plex Sans Arabic"', 'ui-sans-serif', 'system-ui', 'Segoe UI', 'Arial', 'sans-serif'],
      },
    },
  },
  plugins: [],
} satisfies Config;

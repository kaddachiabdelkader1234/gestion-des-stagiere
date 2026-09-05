/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        // STB Brand Colors - Société Tunisienne de Banque
        primary: '#002B5C',      // STB Navy Blue - professional, trustworthy
        accent: '#C8A951',       // STB Gold - premium, banking
        background: '#F5F7FA',   // Light cool gray background
        text: '#1A1D23',         // Dark charcoal text

        // Variations
        'primary-light': '#004A8F',
        'primary-dark': '#001E3D',
        'accent-light': '#D4B96A',
        'accent-dark': '#B8983D',

        // Status colors
        success: '#10B981',
        warning: '#F59E0B',
        danger: '#EF4444',
        info: '#3B82F6',
      },
      boxShadow: {
        'mentor-shadow': '0px 4px 20px rgba(0, 43, 92, 0.08)',
        'card': '0 4px 6px -1px rgba(0, 0, 0, 0.07), 0 2px 4px -1px rgba(0, 0, 0, 0.04)',
        'card-hover': '0 20px 25px -5px rgba(0, 43, 92, 0.1), 0 10px 10px -5px rgba(0, 43, 92, 0.04)',
        'banking': '0 4px 24px rgba(0, 43, 92, 0.12)',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
      },
    },
  },
  plugins: [],
}

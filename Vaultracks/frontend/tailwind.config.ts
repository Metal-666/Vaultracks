// /** @type {import('tailwindcss').Config} */

// export const content = ["./pages/**/*.{html,js}"];

// export const theme = {
// 	extend: {},
// };

// export const plugins = [require("@catppuccin/tailwindcss")];

import type { Config } from "tailwindcss";
import catppuccin from "@catppuccin/tailwindcss";
import tailwindExtendedShadows from "tailwind-extended-shadows";

export default {
	content: ["./pages/**/*.{html,js}"],
	theme: {
		extend: {},
	},
	plugins: [
		catppuccin,
		tailwindExtendedShadows,
	],
} satisfies Config;

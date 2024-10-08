import type { Config } from "tailwindcss";
import catppuccin from "@catppuccin/tailwindcss";
import tailwindExtendedShadows from "tailwind-extended-shadows";

export default {
	content: [
		"./pages/**/*.html",
		"./scripts/**/*.ts",
	],
	theme: {
		extend: {},
	},
	plugins: [
		catppuccin,
		tailwindExtendedShadows,
	],
} satisfies Config;

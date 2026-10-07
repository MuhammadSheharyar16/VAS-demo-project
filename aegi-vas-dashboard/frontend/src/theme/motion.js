export const reducedMotion =
  typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches

export const ease = [0.2, 0.8, 0.2, 1]

export const pageVariants = {
  initial: { opacity: 0, rotateX: 8, y: 24, scale: 0.985 },
  animate: { opacity: 1, rotateX: 0, y: 0, scale: 1, transition: { duration: 0.6, ease, staggerChildren: 0.06 } },
  exit: { opacity: 0, rotateX: -6, y: -16, transition: { duration: 0.25, ease } },
}

export const itemVariants = {
  initial: { opacity: 0, y: 16 },
  animate: { opacity: 1, y: 0, transition: { duration: 0.5, ease } },
}

import clsx from 'clsx'

interface SkeletonProps {
  width?: number | string
  height?: number | string
  radius?: string
  className?: string
}

export function Skeleton({ width = '100%', height = 16, radius, className }: SkeletonProps) {
  return <div className={clsx('skeleton', className)} style={{ width, height, borderRadius: radius }} aria-hidden />
}

/** Card-shaped placeholder for a loading section. */
export function SkeletonCard({ height = 160 }: { height?: number }) {
  return <Skeleton height={height} radius="var(--radius-lg)" />
}

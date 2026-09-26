import { forwardRef, useEffect, useImperativeHandle, useRef } from 'react'

export type SignaturePadHandle = {
  clear: () => void
  isEmpty: () => boolean
  toDataUrl: () => string
}

type SignaturePadProps = {
  disabled?: boolean
  ariaLabel?: string
}

const SignaturePad = forwardRef<SignaturePadHandle, SignaturePadProps>(function SignaturePad(
  { disabled = false, ariaLabel = '電子簽名板' },
  ref,
) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const drawingRef = useRef(false)
  const hasInkRef = useRef(false)
  const lastPointRef = useRef<{ x: number; y: number } | null>(null)

  const fillWhiteBackground = (canvas: HTMLCanvasElement) => {
    const context = canvas.getContext('2d')
    if (!context) return
    context.save()
    context.fillStyle = '#ffffff'
    context.fillRect(0, 0, canvas.width, canvas.height)
    context.restore()
  }

  const clear = () => {
    const canvas = canvasRef.current
    if (!canvas) return
    const context = canvas.getContext('2d')
    if (!context) return
    context.clearRect(0, 0, canvas.width, canvas.height)
    fillWhiteBackground(canvas)
    drawingRef.current = false
    hasInkRef.current = false
    lastPointRef.current = null
  }

  useImperativeHandle(ref, () => ({
    clear,
    isEmpty: () => !hasInkRef.current,
    toDataUrl: () => canvasRef.current?.toDataURL('image/png') ?? '',
  }))

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const resize = () => {
      const rect = canvas.getBoundingClientRect()
      if (rect.width <= 0 || rect.height <= 0) return

      const ratio = Math.max(window.devicePixelRatio || 1, 1)
      const nextWidth = Math.max(1, Math.round(rect.width * ratio))
      const nextHeight = Math.max(1, Math.round(rect.height * ratio))
      if (canvas.width === nextWidth && canvas.height === nextHeight) return

      const snapshot = document.createElement('canvas')
      snapshot.width = canvas.width || nextWidth
      snapshot.height = canvas.height || nextHeight
      const snapshotContext = snapshot.getContext('2d')
      if (snapshotContext && canvas.width > 0 && canvas.height > 0) {
        snapshotContext.drawImage(canvas, 0, 0)
      }

      canvas.width = nextWidth
      canvas.height = nextHeight
      const context = canvas.getContext('2d')
      if (!context) return
      context.fillStyle = '#ffffff'
      context.fillRect(0, 0, nextWidth, nextHeight)
      if (hasInkRef.current && snapshot.width > 0 && snapshot.height > 0) {
        context.drawImage(snapshot, 0, 0, snapshot.width, snapshot.height, 0, 0, nextWidth, nextHeight)
      }
    }

    resize()
    const observer = new ResizeObserver(resize)
    observer.observe(canvas)
    return () => observer.disconnect()
  }, [])

  const getPoint = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current
    if (!canvas) return null
    const rect = canvas.getBoundingClientRect()
    if (rect.width <= 0 || rect.height <= 0) return null
    return {
      x: (event.clientX - rect.left) * (canvas.width / rect.width),
      y: (event.clientY - rect.top) * (canvas.height / rect.height),
    }
  }

  const handlePointerDown = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (disabled) return
    const point = getPoint(event)
    if (!point) return
    event.currentTarget.setPointerCapture(event.pointerId)
    drawingRef.current = true
    lastPointRef.current = point
  }

  const handlePointerMove = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (disabled || !drawingRef.current) return
    const canvas = canvasRef.current
    const previous = lastPointRef.current
    const point = getPoint(event)
    if (!canvas || !previous || !point) return

    const context = canvas.getContext('2d')
    if (!context) return
    const ratio = Math.max(window.devicePixelRatio || 1, 1)
    context.beginPath()
    context.moveTo(previous.x, previous.y)
    context.lineTo(point.x, point.y)
    context.strokeStyle = '#111827'
    context.lineWidth = 2.25 * ratio
    context.lineCap = 'round'
    context.lineJoin = 'round'
    context.stroke()

    lastPointRef.current = point
    hasInkRef.current = true
  }

  const stopDrawing = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (drawingRef.current && event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId)
    }
    drawingRef.current = false
    lastPointRef.current = null
  }

  return (
    <canvas
      ref={canvasRef}
      className={`signature-pad${disabled ? ' signature-pad-disabled' : ''}`}
      aria-label={ariaLabel}
      role="img"
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={stopDrawing}
      onPointerCancel={stopDrawing}
      onPointerLeave={(event) => {
        if (event.buttons === 0) stopDrawing(event)
      }}
    />
  )
})

export default SignaturePad

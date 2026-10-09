import { useMemo, useRef, useState } from 'react'
import { Canvas, useFrame, useThree } from '@react-three/fiber'
import { OrbitControls } from '@react-three/drei'
import * as THREE from 'three'
import { reducedMotion } from '../../theme/motion'
import { fmtMoney } from '../../utils/format'

// Country name drawn to a canvas texture, so labels need no font download.
function Label({ text }) {
  const texture = useMemo(() => {
    const c = document.createElement('canvas')
    c.width = 512; c.height = 128
    const ctx = c.getContext('2d')
    ctx.font = '600 64px Manrope, system-ui, sans-serif'
    ctx.fillStyle = '#e8edf6'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'
    ctx.fillText(text, 256, 64)
    const t = new THREE.CanvasTexture(c)
    t.anisotropy = 4
    return t
  }, [text])
  return (
    <mesh position={[0, 0.02, 0.95]} rotation={[-Math.PI / 2, 0, 0]}>
      <planeGeometry args={[2, 0.5]} />
      <meshBasicMaterial map={texture} transparent depthWrite={false} />
    </mesh>
  )
}

function Bar({ x, z, height, color, delay, label, onHover, active }) {
  const ref = useRef()
  // Box with its base on the floor, so scaling Y grows the bar upwards.
  const geometry = useMemo(() => new THREE.BoxGeometry(0.8, 1, 0.8).translate(0, 0.5, 0), [])
  const start = useRef(null)
  useFrame(({ clock }) => {
    if (!ref.current) return
    if (start.current == null) start.current = clock.elapsedTime
    const p = reducedMotion ? 1 : Math.min(1, Math.max(0, (clock.elapsedTime - start.current - delay) / 1.1))
    ref.current.scale.y = Math.max(0.001, height * (1 - Math.pow(1 - p, 3)))
  })
  return (
    <group position={[x, 0, z]}>
      <mesh ref={ref} geometry={geometry} scale={[1, 0.001, 1]}
        onPointerOver={(e) => { e.stopPropagation(); onHover() }} onPointerOut={() => onHover(null)}>
        <meshStandardMaterial color={color} metalness={0.65} roughness={0.28} emissive={color} emissiveIntensity={active ? 0.55 : 0.12} />
      </mesh>
      <Label text={label} />
    </group>
  )
}

// Pull the camera back on narrow screens so every bar stays in view.
function FitCamera() {
  const { camera, size } = useThree()
  const aspect = size.width / size.height
  camera.position.setLength(aspect < 1.4 ? 12.5 * (1.4 / Math.max(aspect, 0.6)) : 12.5)
  camera.updateProjectionMatrix()
  return null
}

function Scene({ bars, onHover, hovered }) {
  const max = Math.max(...bars.map((c) => c.revenue), 1)
  const gap = 1.35, ox = -((bars.length - 1) * gap) / 2
  const colors = useMemo(() => bars.map((_, i) => new THREE.Color().setHSL(0.11, 0.62, 0.6 - (i / Math.max(1, bars.length - 1)) * 0.22)), [bars])
  return (
    <>
      <ambientLight color="#8ea4cc" intensity={1.2} />
      <directionalLight position={[5, 9, 6]} intensity={2.4} color="#fff1cf" />
      <pointLight position={[-6, 4, -4]} intensity={20} color="#6aa8e4" />
      <mesh rotation={[-Math.PI / 2, 0, 0]}>
        <circleGeometry args={[Math.max(6.4, bars.length * 0.85), 64]} />
        <meshStandardMaterial color="#1c3260" metalness={0.3} roughness={0.7} transparent opacity={0.85} />
      </mesh>
      {bars.map((c, i) => (
        <Bar key={c.label} x={ox + i * gap} z={Math.sin(i * 1.3) * 0.9} height={(c.revenue / max) * 4.5}
          color={colors[i]} delay={i * 0.11} label={c.label} active={hovered === c.label}
          onHover={(on) => onHover(on === null ? null : c.label)} />
      ))}
    </>
  )
}

export default function RevenueBars3D({ bars, currency }) {
  const [hovered, setHovered] = useState(null)
  const shown = (hovered && bars.find((c) => c.label === hovered)) || bars[0]
  if (!bars.length) return <div className="empty">No data for the selected filters.</div>
  return (
    <>
      <div className="three" aria-hidden="true">
        <Canvas camera={{ position: [0, 6, 12.5], fov: 38 }} dpr={[1, 2]}>
          <FitCamera />
          <Scene bars={bars} onHover={setHovered} hovered={hovered} />
          <OrbitControls enablePan={false} enableZoom={false} autoRotate={!reducedMotion && !hovered} autoRotateSpeed={0.6}
            minPolarAngle={0.5} maxPolarAngle={1.35} target={[0, 1.4, 0]} />
        </Canvas>
      </div>
      <div className="tip">
        Drag to rotate · hover a bar for details · <b>{shown.label} · {fmtMoney(shown.revenue, currency)}{shown.sharePercent != null && ` · ${shown.sharePercent.toFixed(1)}% of revenue`}</b>
        {shown.centres?.length > 1 && <> · {shown.centres.map((c) => `${c.centre} ${fmtMoney(c.revenue, currency)}`).join(', ')}</>}
      </div>
    </>
  )
}

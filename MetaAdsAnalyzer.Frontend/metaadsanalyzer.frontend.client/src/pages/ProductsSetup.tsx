import { useEffect, useState } from 'react'
import { createProduct, getProducts } from '../api/client'
import type { CreateProductPayload, ProductResponse } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

const emptyForm: Omit<CreateProductPayload, 'userId'> = {
  name: '',
  cogs: 0,
  sellingPrice: 0,
  shippingCost: 0,
  paymentFeePct: 2.9,
  returnRatePct: 0,
  ltvMultiplier: 1,
  targetMarginPct: 20,
}

export function ProductsSetup() {
  const { userId } = useUser()
  const [list, setList] = useState<ProductResponse[]>([])
  const [form, setForm] = useState(emptyForm)
  const [err, setErr] = useState<string | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  async function refresh() {
    const data = await getProducts(userId)
    setList(data)
  }

  useEffect(() => {
    let c = false
    setLoading(true)
    getProducts(userId)
      .then((d) => {
        if (!c) setList(d)
      })
      .catch((e: unknown) => {
        if (!c) setErr(e instanceof Error ? e.message : 'Liste hatası')
      })
      .finally(() => {
        if (!c) setLoading(false)
      })
    return () => {
      c = true
    }
  }, [userId])

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    setMsg(null)
    try {
      await createProduct({ userId, ...form, name: form.name.trim() })
      setForm(emptyForm)
      setMsg('Ürün eklendi.')
      await refresh()
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Kayıt hatası')
    }
  }

  return (
    <div className="page">
      <h1 className="page-title">Ürün maliyetleri</h1>
      <p className="page-lead">
        COGS, fiyat ve hedef marj ile kârlılık eşikleri hesaplanır. Yüzdeler ondalık olarak (örn. 2.9 =
        %2,9).
      </p>

      {err && <p className="error-banner">{err}</p>}
      {msg && <p className="ok-banner">{msg}</p>}

      <section className="panel form-stack">
        <h2 className="panel-title">Yeni ürün</h2>
        <form onSubmit={onSubmit}>
          <label>
            Ürün adı
            <input
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              required
            />
          </label>
          <div className="form-grid-2">
            <label>
              COGS
              <input
                type="number"
                step="0.01"
                min={0}
                value={form.cogs}
                onChange={(e) => setForm((f) => ({ ...f, cogs: Number(e.target.value) }))}
              />
            </label>
            <label>
              Satış fiyatı
              <input
                type="number"
                step="0.01"
                min={0}
                value={form.sellingPrice}
                onChange={(e) => setForm((f) => ({ ...f, sellingPrice: Number(e.target.value) }))}
                required
              />
            </label>
            <label>
              Kargo
              <input
                type="number"
                step="0.01"
                min={0}
                value={form.shippingCost}
                onChange={(e) => setForm((f) => ({ ...f, shippingCost: Number(e.target.value) }))}
              />
            </label>
            <label>
              Ödeme komisyonu %
              <input
                type="number"
                step="0.01"
                min={0}
                value={form.paymentFeePct}
                onChange={(e) => setForm((f) => ({ ...f, paymentFeePct: Number(e.target.value) }))}
              />
            </label>
            <label>
              İade oranı %
              <input
                type="number"
                step="0.01"
                min={0}
                value={form.returnRatePct}
                onChange={(e) => setForm((f) => ({ ...f, returnRatePct: Number(e.target.value) }))}
              />
            </label>
            <label>
              LTV çarpanı
              <input
                type="number"
                step="0.01"
                min={0.01}
                value={form.ltvMultiplier}
                onChange={(e) => setForm((f) => ({ ...f, ltvMultiplier: Number(e.target.value) }))}
              />
            </label>
            <label>
              Hedef net marj %
              <input
                type="number"
                step="0.01"
                min={0}
                value={form.targetMarginPct}
                onChange={(e) => setForm((f) => ({ ...f, targetMarginPct: Number(e.target.value) }))}
              />
            </label>
          </div>
          <button type="submit" className="btn primary">
            Ürün ekle
          </button>
        </form>
      </section>

      <section className="panel">
        <h2 className="panel-title">Kayıtlı ürünler</h2>
        {loading && <p className="muted">Yükleniyor…</p>}
        {!loading && list.length === 0 && <p className="muted">Henüz ürün yok.</p>}
        {!loading && list.length > 0 && (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Ad</th>
                  <th>Fiyat</th>
                  <th>COGS</th>
                  <th>Hedef marj %</th>
                </tr>
              </thead>
              <tbody>
                {list.map((p) => (
                  <tr key={p.id}>
                    <td>{p.name}</td>
                    <td>{p.sellingPrice}</td>
                    <td>{p.cogs}</td>
                    <td>{p.targetMarginPct}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

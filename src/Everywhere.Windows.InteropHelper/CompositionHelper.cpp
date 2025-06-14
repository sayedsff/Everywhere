#include <windows.h>
#ifdef GetCurrentTime
#undef GetCurrentTime
#endif

#include "d2d1.h"
#include <d2d1helper.h>

#include <winrt/Windows.UI.Composition.Desktop.h>
#include <winrt/Windows.Graphics.h>
#include <windows.graphics.interop.h>

using namespace winrt;
using namespace Windows::Foundation::Numerics;

// https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/blob/master/cpp/HelloVectors/HelloVectors_win32.cpp#L134
// Helper class for converting geometry to a composition-compatible geometry source.
struct D2D1GeometrySource : implements<D2D1GeometrySource,
    Windows::Graphics::IGeometrySource2D,
    ABI::Windows::Graphics::IGeometrySource2DInterop>
{
    explicit D2D1GeometrySource(com_ptr<ID2D1Geometry> const& pGeometry) :
        geometry(pGeometry)
    {
    }

    IFACEMETHODIMP GetGeometry(ID2D1Geometry** value) override
    {
        geometry.copy_to(value);
        return S_OK;
    }

    IFACEMETHODIMP TryGetGeometryUsingFactory(ID2D1Factory*, ID2D1Geometry** result) override
    {
        *result = nullptr;
        return E_NOTIMPL;
    }

private:
    com_ptr<ID2D1Geometry> geometry;
};

EXTERN_C __declspec(dllexport) HRESULT CreateComplexRoundedRectangleCompositionPath(
    const float width,
    const float height,
    const float topLeft,
    const float topRight,
    const float bottomRight,
    const float bottomLeft,
    __out IInspectable** ppCompositionPath)
{
    HRESULT hr;
    *ppCompositionPath = nullptr;

    com_ptr<ID2D1Factory> factory;
    if (FAILED(hr = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, factory.put())))
    {
        return hr;
    }

    com_ptr<ID2D1PathGeometry> geometry;
    if (FAILED(hr = factory->CreatePathGeometry(geometry.put())))
    {
        return hr;
    }

    com_ptr<ID2D1GeometrySink> sink;
    if (FAILED(hr = geometry->Open(sink.put())))
    {
        return hr;
    }

    sink->SetFillMode(D2D1_FILL_MODE_WINDING);
    sink->BeginFigure(D2D1::Point2F(topLeft, 0.0f), D2D1_FIGURE_BEGIN_FILLED);

    // Top line and top-right arc
    sink->AddLine(D2D1::Point2F(width - topRight, 0.0f));
    sink->AddArc(D2D1::ArcSegment(
        D2D1::Point2F(width, topRight),
        D2D1::SizeF(topRight, topRight),
        0.0f,
        D2D1_SWEEP_DIRECTION_CLOCKWISE,
        D2D1_ARC_SIZE_SMALL));

    // Right line and bottom-right arc
    sink->AddLine(D2D1::Point2F(width, height - bottomRight));
    sink->AddArc(D2D1::ArcSegment(
        D2D1::Point2F(width - bottomRight, height),
        D2D1::SizeF(bottomRight, bottomRight),
        0.0f,
        D2D1_SWEEP_DIRECTION_CLOCKWISE,
        D2D1_ARC_SIZE_SMALL));

    // Bottom line and bottom-left arc
    sink->AddLine(D2D1::Point2F(bottomLeft, height));
    sink->AddArc(D2D1::ArcSegment(
        D2D1::Point2F(0.0f, height - bottomLeft),
        D2D1::SizeF(bottomLeft, bottomLeft),
        0.0f,
        D2D1_SWEEP_DIRECTION_CLOCKWISE,
        D2D1_ARC_SIZE_SMALL));

    // Left line and top-left arc
    sink->AddLine(D2D1::Point2F(0.0f, topLeft));
    sink->AddArc(D2D1::ArcSegment(
        D2D1::Point2F(topLeft, 0.0f),
        D2D1::SizeF(topLeft, topLeft),
        0.0f,
        D2D1_SWEEP_DIRECTION_CLOCKWISE,
        D2D1_ARC_SIZE_SMALL));

    sink->EndFigure(D2D1_FIGURE_END_CLOSED);
    if (FAILED(hr = sink->Close()))
    {
        return hr;
    }

    init_apartment(apartment_type::single_threaded);

    const auto compositionPath = Windows::UI::Composition::CompositionPath(make<D2D1GeometrySource>(geometry));
    *ppCompositionPath = static_cast<IInspectable *>(get_abi(compositionPath));
    (*ppCompositionPath)->AddRef(); // Ensure the returned object is reference counted
    return S_OK;
}
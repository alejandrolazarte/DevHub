window.canvasInterop = (function () {
    let cy = null;
    let dotNetRef = null;

    const roleColors = {
        DefineClass: '#c084fc',
        DefineInterface: '#60a5fa',
        DefineMethod: '#34d399',
        DefineProperty: '#a3e635',
        Implements: '#fb923c',
        UsesImport: '#94a3b8',
        UsesInstance: '#f472b6',
        UsesParameter: '#e2e8f0',
        Unknown: '#475569',
    };

    function primaryColor(roles) {
        const priority = ['DefineClass', 'DefineInterface', 'DefineMethod', 'DefineProperty', 'Implements', 'UsesInstance', 'UsesParameter', 'UsesImport'];
        for (const r of priority) {
            if (roles && roles.includes(r)) return roleColors[r];
        }
        return roleColors.Unknown;
    }

    return {
        init(containerId, ref) {
            dotNetRef = ref;
            const container = document.getElementById(containerId);
            if (!container) return;

            cy = cytoscape({
                container,
                style: [
                    {
                        selector: 'node',
                        style: {
                            'label': 'data(label)',
                            'background-color': (ele) => primaryColor(ele.data('roles')),
                            'color': '#f8fafc',
                            'text-valign': 'center',
                            'text-halign': 'center',
                            'font-size': '11px',
                            'width': 'label',
                            'height': 'label',
                            'padding': '10px',
                            'shape': 'roundrectangle',
                            'border-width': 2,
                            'border-color': '#1e293b',
                            'text-wrap': 'wrap',
                            'text-max-width': '120px',
                        }
                    },
                    {
                        selector: 'node:selected',
                        style: {
                            'border-color': '#f8fafc',
                            'border-width': 3,
                        }
                    },
                    {
                        selector: 'node[type="label"]',
                        style: {
                            'background-color': '#1e293b',
                            'border-color': '#475569',
                            'color': '#cbd5e1',
                            'shape': 'rectangle',
                        }
                    },
                    {
                        selector: 'edge',
                        style: {
                            'width': 1.5,
                            'line-color': '#475569',
                            'target-arrow-color': '#475569',
                            'target-arrow-shape': 'triangle',
                            'curve-style': 'bezier',
                            'label': 'data(label)',
                            'font-size': '9px',
                            'color': '#94a3b8',
                            'text-background-color': '#0f172a',
                            'text-background-opacity': 0.8,
                            'text-background-padding': '2px',
                        }
                    },
                    {
                        selector: 'edge[type="manual"]',
                        style: {
                            'line-style': 'dashed',
                            'line-color': '#7c3aed',
                            'target-arrow-color': '#7c3aed',
                        }
                    }
                ],
                layout: { name: 'cose', padding: 40, nodeRepulsion: 8000 },
                wheelSensitivity: 0.3,
                maxZoom: 2,
            });

            cy.on('tap', 'node', function (evt) {
                const id = evt.target.data('id');
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnNodeClicked', id);
                }
            });

            cy.on('tap', function (evt) {
                if (evt.target === cy && dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnNodeClicked', '');
                }
            });
        },

        setGraph(elementsJson) {
            if (!cy) return;
            const elements = JSON.parse(elementsJson);
            cy.elements().remove();
            cy.add(elements);
            const layout = cy.layout({ name: 'cose', padding: 40, nodeRepulsion: 8000 });
            layout.on('layoutstop', () => {
                if (cy.zoom() > 1.5) { cy.zoom(1.5); cy.center(); }
            });
            layout.run();
        },

        getGraph() {
            if (!cy) return '{}';
            return JSON.stringify(cy.json().elements);
        },

        loadGraph(elementsJson) {
            if (!cy) return;
            cy.elements().remove();
            cy.add(JSON.parse(elementsJson));
            cy.fit();
        },

        addLabelNode() {
            if (!cy) return;
            const id = 'label-' + Date.now();
            cy.add({ data: { id, label: 'Nota', type: 'label' } });
            cy.layout({ name: 'preset' }).run();
        },

        fitAll() {
            if (cy) cy.fit();
        },

        getNodePositions(containerId) {
            if (!cy) return [];
            const container = document.getElementById(containerId);
            if (!container) return [];
            const rect = container.getBoundingClientRect();
            return cy.nodes().map(n => {
                const bb = n.renderedBoundingBox();
                return {
                    id: n.data('id'),
                    label: n.data('label'),
                    x: rect.left + (bb.x1 + bb.x2) / 2,
                    y: rect.top + (bb.y1 + bb.y2) / 2
                };
            });
        },

        destroy() {
            if (cy) {
                cy.destroy();
                cy = null;
            }
            dotNetRef = null;
        }
    };
})();
